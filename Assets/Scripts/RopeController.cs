using UnityEngine;
using System.Collections;
using Obi;
using DG.Tweening;

/// <summary>
/// Obi 绳索管理器：运行时初始化 + Pin 约束 + 实时调参 + 断裂检测。
///
/// 前置条件（Rope.prefab 已具备大部分）：
/// - ObiSolver + ObiRope + ObiCurve + ObiRopeCursor + ObiRopeExtrudedRenderer
/// - Player 上有 ObiCollider
/// - 场景中拖好 player1Collider / player2Collider / config / gameManager 引用
/// </summary>
public class RopeController : MonoBehaviour
{
    [Header("组件引用")]
    [Tooltip("场景中的 ObiRope 组件")]
    public ObiRope rope;
    [Tooltip("场景中的 ObiSolver 组件")]
    public ObiSolver solver;
    [Tooltip("场景中的 ObiRopeCursor 组件")]
    public ObiRopeCursor cursor;

    [Header("玩家（需要有 ObiCollider 组件）")]
    [Tooltip("玩家 1 的 ObiCollider（ObiCollider 或 ObiCollider2D）")]
    public ObiColliderBase player1Collider;
    [Tooltip("玩家 2 的 ObiCollider（ObiCollider 或 ObiCollider2D）")]
    public ObiColliderBase player2Collider;
    [Tooltip("Pin 约束偏移。调 Y 值把绳从脚底提到角色中心")]
    public Vector3 pinOffset = Vector3.zero;

    [Header("配置")]
    [Tooltip("绳索物理参数配置（RopeConfig 资产）")]
    public RopeConfig config;

    [Header("事件")]
    [Tooltip("GameManager 引用，断裂时回调 OnRopeBreak()")]
    public GameManager gameManager;

    [Header("端点同步")]
    [Tooltip("端点每帧最大同步速度，防止击飞时绳子瞬间拉长变形")]
    public float maxEndpointSnapSpeed = 30f;

    [Header("传输特效")]
    [Tooltip("绳子渲染器（拖入 MeshRenderer 或 Obi 渲染器组件）")]
    public Renderer ropeRenderer;
    [Tooltip("传输时绳子的目标颜色")]
    public Color transferColor = new Color(1f, 0.8f, 0.2f, 1f);
    [Tooltip("传输颜色动画时长")]
    public float transferColorDuration = 0.3f;

    // 调参脏标记：避免每帧 ChangeLength（增删粒子是重操作）
    float lastAppliedLength = -1f;
    float lastAppliedStretchStiffness = -1f;
    float lastAppliedBendingStiffness = -1f;
    float lastAppliedTearResistance = -1f;

    bool ropeBroken;
    bool pinsSetup;
    int lastUsedParticles;
    bool isTransferEffectActive;
    Color originalRopeColor;
    Material ropeMaterialInstance;
    Tweener transferTweener;

    Rigidbody2D rb1;
    Rigidbody2D rb2;
    PlayerController pc1;
    PlayerController pc2;

    void Awake()
    {
        if (rope == null)   rope = GetComponent<ObiRope>();
        if (solver == null) solver = GetComponent<ObiSolver>();
        if (cursor == null) cursor = GetComponent<ObiRopeCursor>();
    }

    IEnumerator Start()
    {
        // 等一帧，确保所有组件 Awake/Start 跑完
        yield return null;

        if (rope == null) yield break;

        // === 运行时初始化 ===
        // 如果 prefab 里的 rope 还没初始化（initialized=0，粒子未生成），
        // 则手动生成物理表示并加入 solver。
        if (!rope.Initialized)
        {
            yield return StartCoroutine(rope.GeneratePhysicRepresentationForMesh());
            rope.AddToSolver(null);
        }

        // ★ 太空环境：零重力（必须 Push 到原生层，否则 struct field 修改不生效）
        if (solver != null)
        {
            solver.parameters.gravity = Vector4.zero;
            solver.UpdateParameters();
        }

        // ★ 初始刚度从 config 读取（软绳）
        rope.BendingConstraints.stiffness = config != null ? config.bendingStiffness : 0.438f;
        rope.DistanceConstraints.stiffness = 1f;

        // ★ 缓存 Rigidbody2D（必须在 SetupPins 之前，用于冻结玩家）
        rb1 = player1Collider != null ? player1Collider.GetComponent<Rigidbody2D>() : null;
        rb2 = player2Collider != null ? player2Collider.GetComponent<Rigidbody2D>() : null;
        pc1 = rb1 != null ? rb1.GetComponent<PlayerController>() : null;
        pc2 = rb2 != null ? rb2.GetComponent<PlayerController>() : null;

        // ★ 冻结玩家 → Obi pin 约束无法移动 kinematic 物体 → 绳子自己在两人之间就位
        if (rb1 != null) rb1.isKinematic = true;
        if (rb2 != null) rb2.isKinematic = true;

        // === Pin 约束：粒子 0 → P1，粒子 N → P2 ===
        SetupPins();

        // ★ 等绳子在冻结的玩家之间自然就位（Obi 需要几帧物理步）
        yield return new WaitForSeconds(0.2f);
        yield return new WaitForFixedUpdate();

        // ★ 解冻玩家，归零残留速度
        if (rb1 != null) { rb1.isKinematic = false; rb1.velocity = Vector2.zero; }
        if (rb2 != null) { rb2.isKinematic = false; rb2.velocity = Vector2.zero; }

        lastUsedParticles = rope.UsedParticles;

        // 强制触发首次参数应用
        lastAppliedLength = -1f;
        lastAppliedStretchStiffness = -1f;
        lastAppliedBendingStiffness = -1f;
        lastAppliedTearResistance = -1f;

        ApplyConfigIfChanged();
    }

    void SetupPins()
    {
        if (rope == null || !rope.Initialized) return;
        if (pinsSetup) return;

        // ★ 把玩家 ObiCollider2D 的碰撞源换成微型 CircleCollider2D
        // 原来的 CapsuleCollider2D 形状太大 → 绳索粒子碰撞后绕圈
        // 换成 r=0.01 的圆形后，Obi 碰撞形状极小，绳索无法"绕"住玩家身体
        // Unity 物理碰撞（CapsuleCollider2D 之间）不受影响
        SwapToTinyCollider(player1Collider);
        SwapToTinyCollider(player2Collider);

        // 获取 Pin 约束批次（Initialize 里已创建了一个空 batch）
        ObiPinConstraintBatch batch = (ObiPinConstraintBatch)rope.PinConstraints.GetFirstBatch();
        if (batch == null)
        {
            batch = new ObiPinConstraintBatch(false, false, 0, 1);
            rope.PinConstraints.AddBatch(batch);
        }

        int particleCount = rope.UsedParticles;

        // 粒子 0 → Player1
        if (player1Collider != null && particleCount > 0)
        {
            batch.AddConstraint(0, player1Collider, pinOffset, Quaternion.identity, 1);
        }

        // 粒子 N → Player2
        if (player2Collider != null && particleCount > 1)
        {
            batch.AddConstraint(particleCount - 1, player2Collider, pinOffset, Quaternion.identity, 1);
        }

        // 重新注册到 solver 使 pin 生效
        if (rope.InSolver)
        {
            rope.PinConstraints.RemoveFromSolver(null);
            rope.PinConstraints.AddToSolver(null);
        }

        pinsSetup = true;
    }

    /// <summary>
    /// 把 ObiCollider2D 的 source collider 从大的 CapsuleCollider2D 换成微型 CircleCollider2D。
    /// 只影响 Obi 碰撞形状，不影响 Unity 物理。
    /// </summary>
    void SwapToTinyCollider(ObiColliderBase obiCol)
    {
        if (obiCol == null) return;

        ObiCollider2D col2D = obiCol as ObiCollider2D;
        if (col2D == null) return;

        // 检查是否已经换过（避免重复创建）
        CircleCollider2D existing = col2D.SourceCollider as CircleCollider2D;
        if (existing != null && existing.radius < 0.1f) return; // 已经是微型的

        // 在玩家 GameObject 上加一个微型 CircleCollider2D（只给 Obi 做碰撞形状用）
        CircleCollider2D tiny = col2D.gameObject.AddComponent<CircleCollider2D>();
        tiny.radius = 0.01f;

        // ★ 关键：把 ObiCollider2D 的 source 换成微型碰撞体
        // SourceCollider setter 内部会 Recreate 原生碰撞体（RemoveCollider + AddCollider）
        col2D.SourceCollider = tiny;
        col2D.Phase = 2;
        col2D.Thickness = 0f;
    }

    void Update()
    {
        if (rope != null)
        {
            // 检测断裂（UsedParticles 减少说明有粒子被撕裂）
            if (!ropeBroken && rope.UsedParticles < lastUsedParticles && lastUsedParticles > 0)
            {
                ropeBroken = true;
                if (gameManager != null)
                    gameManager.OnRopeBreak();
            }
            lastUsedParticles = rope.UsedParticles;
        }

        // Play Mode 实时调参（只有值变化时才 apply）
        ApplyConfigIfChanged();
    }

    /// <summary>
    /// 弹簧拉力：玩家间距超过基准绳长时双向拉回。
    /// 绳子不主动调 cursor 伸长——Obi pin 约束 + 距离约束自然拉伸。
    ///
    /// ★ 击飞恢复：绳子严重超伸（>1.5x 绳长）时临时提高求解器迭代次数，
    /// 帮助中间粒子快速追上端点位移，恢复自然绳形。
    /// </summary>
    void FixedUpdate()
    {
        if (rb1 == null || rb2 == null || config == null) return;

        Vector3 p1 = rb1.transform.position;
        Vector3 p2 = rb2.transform.position;
        float dist = Vector3.Distance(p1, p2);

        // ★ 击飞恢复：严重超伸时临时提升距离约束迭代
        if (solver != null && config.ropeLength > 0f)
        {
            float stretchRatio = dist / config.ropeLength;
            if (stretchRatio > 1.5f)
            {
                // 紧急迭代：正常 3~10 次 → 击飞时 20~50 次
                int emergencyIters = Mathf.RoundToInt(Mathf.Min(stretchRatio * 10f, 50f));
                solver.distanceConstraintParameters.iterations = emergencyIters;
            }
            else
            {
                // 恢复正常迭代数
                int normalIters = Mathf.RoundToInt(config.stretchStiffness * 10f);
                if (solver.distanceConstraintParameters.iterations != Mathf.Max(1, normalIters))
                    solver.distanceConstraintParameters.iterations = Mathf.Max(1, normalIters);
            }
        }

        if (dist > config.ropeLength)
        {
            Vector3 dir = (p2 - p1).normalized;
            float stretch = dist - config.ropeLength;
            float force = stretch * config.stretchStiffness * 50f;

            // 双向弹簧
            rb1.AddForce(dir * force, ForceMode2D.Force);
            rb2.AddForce(-dir * force, ForceMode2D.Force);
        }
    }

    /// <summary>
    /// ★ 强制同步 pinned 粒子到玩家位置，消除 Obi solver 对 MovePosition 的 1-2 帧延迟。
    /// 表面行走用 rb.MovePosition（瞬移），Obi solver 在 FixedUpdate 求解后，
    /// 相邻粒子需要多个迭代才能追上瞬移量 → 绳端产生甩动/拉长闪烁。
    /// 这里在渲染前直接矫正端粒子位置，绕过 solver 延迟。
    ///
    /// ★ 击飞保护：非吸附时端点每帧移动距离上限 = maxEndpointSnapSpeed * dt。
    /// 陨石击飞时玩家瞬间位移巨大，若端点无限制 snap 会导致绳子拉长变形。
    /// 限制 snap 速度后，求解器有足够时间把位移传播到中间粒子。
    ///
    /// ★ 吸附时强制同步：表面移动是 MovePosition 瞬移，若端点也被限速会跟不上，
    /// 绳子拖拽玩家导致卡住。吸附状态下直接设为目标位置，不做限速。
    /// </summary>
    void LateUpdate()
    {
        if (rope == null || !rope.Initialized || ropeBroken) return;

        var pos = rope.positions;
        var vel = rope.velocities;
        float maxDelta = maxEndpointSnapSpeed * Time.deltaTime;

        // 粒子 0 → Player1（用 rb.position 避免 Rigidbody2D Interpolate 导致的插值偏差）
        if (player1Collider != null && rb1 != null && pos.Length > 0)
        {
            Vector3 targetWorld = (Vector3)rb1.position + pinOffset;
            Vector3 currentWorld = rope.transform.TransformPoint(pos[0]);
            // ★ 吸附状态：强制同步不下滑，不走限速；非吸附：限速防击飞拉长
            bool anchored = pc1 != null && pc1.IsAnchored;
            Vector3 clampedWorld = anchored
                ? targetWorld
                : Vector3.MoveTowards(currentWorld, targetWorld, maxDelta);
            pos[0] = rope.transform.InverseTransformPoint(clampedWorld);
            if (vel.Length > 0) vel[0] = Vector3.zero;
        }

        // 粒子 N → Player2（同上）
        if (player2Collider != null && rb2 != null && pos.Length > 1)
        {
            int idx = rope.UsedParticles - 1;
            if (idx < pos.Length)
            {
                Vector3 targetWorld = (Vector3)rb2.position + pinOffset;
                Vector3 currentWorld = rope.transform.TransformPoint(pos[idx]);
                // ★ 吸附状态：强制同步不下滑，不走限速
                bool anchored = pc2 != null && pc2.IsAnchored;
                Vector3 clampedWorld = anchored
                    ? targetWorld
                    : Vector3.MoveTowards(currentWorld, targetWorld, maxDelta);
                pos[idx] = rope.transform.InverseTransformPoint(clampedWorld);
                if (vel.Length > idx) vel[idx] = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// 只有参数真正变化时才应用。ChangeLength 只在绳长变化时调用（避免每帧增删粒子）。
    /// </summary>
    void ApplyConfigIfChanged()
    {
        if (rope == null || config == null) return;

        // 绳子必须已初始化（有粒子 + 约束批次）
        if (!rope.Initialized) return;

        // --- 绳长（只在值变化时调 ChangeLength）---
        if (!Mathf.Approximately(config.ropeLength, lastAppliedLength))
        {
            lastAppliedLength = config.ropeLength;
            if (cursor != null)
                cursor.ChangeLength(config.ropeLength);
        }

        // --- 弯曲刚度 ---
        if (!Mathf.Approximately(config.bendingStiffness, lastAppliedBendingStiffness))
        {
            lastAppliedBendingStiffness = config.bendingStiffness;
            rope.BendingConstraints.stiffness = config.bendingStiffness;
        }

        // --- 拉伸刚度（影响 solver 迭代次数）---
        if (!Mathf.Approximately(config.stretchStiffness, lastAppliedStretchStiffness))
        {
            lastAppliedStretchStiffness = config.stretchStiffness;
            if (solver != null)
            {
                int iters = Mathf.RoundToInt(config.stretchStiffness * 10f);
                solver.distanceConstraintParameters.iterations = Mathf.Max(1, iters);
            }
        }

        // --- 弯曲迭代次数 ---
        if (solver != null && !Mathf.Approximately(config.bendingStiffness, lastAppliedBendingStiffness))
        {
            int bendIters = Mathf.RoundToInt(config.bendingStiffness * 10f);
            solver.bendingConstraintParameters.iterations = Mathf.Max(1, bendIters);
        }

        // --- 断裂 ---
        if (!Mathf.Approximately(config.tearResistance, lastAppliedTearResistance))
        {
            lastAppliedTearResistance = config.tearResistance;
            rope.tearable = config.tearResistance < 999f;
            if (rope.tearable)
                rope.tearResistanceMultiplier = config.tearResistance;
        }
    }

    /// <summary>GameManager 重来后调用，重置断裂状态</summary>
    public void ResetRope()
    {
        ropeBroken = false;
        if (rope != null)
            lastUsedParticles = rope.UsedParticles;
    }

    /// <summary>传输能量时绳子变色特效。PlayerController 按住/松开传输键时调用。</summary>
    public void SetTransferEffect(bool active)
    {
        // 懒加载：首次取绳子材质实例
        if (ropeMaterialInstance == null)
        {
            // 优先用手动拖入的 renderer，否则自动找
            if (ropeRenderer == null)
                ropeRenderer = GetComponent<Renderer>();
            if (ropeRenderer == null) return;

            ropeMaterialInstance = ropeRenderer.material;
            originalRopeColor = ropeMaterialInstance.color;
        }

        if (active && !isTransferEffectActive)
        {
            isTransferEffectActive = true;
            transferTweener?.Kill();
            transferTweener = ropeMaterialInstance
                .DOColor(transferColor, transferColorDuration)
                .SetEase(Ease.OutQuad);
        }
        else if (!active && isTransferEffectActive)
        {
            isTransferEffectActive = false;
            transferTweener?.Kill();
            transferTweener = ropeMaterialInstance
                .DOColor(originalRopeColor, transferColorDuration)
                .SetEase(Ease.OutQuad);
        }
    }
}
