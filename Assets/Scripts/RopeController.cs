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

    [Header("玩家（需要有 ObiCollider 组件）")]
    [Tooltip("玩家 1 的 ObiCollider（ObiCollider 或 ObiCollider2D）")]
    public ObiColliderBase player1Collider;
    [Tooltip("玩家 2 的 ObiCollider（ObiCollider 或 ObiCollider2D）")]
    public ObiColliderBase player2Collider;
    [Tooltip("Pin 约束偏移。调 Y 值把绳从脚底提到角色中心")]
    public Vector3 pinOffset = Vector3.zero;

    [Header("玩家控制器")]
    [Tooltip("玩家 1 的 PlayerController，用于读 IsPulling")]
    public PlayerController player1Ctrl;
    [Tooltip("玩家 2 的 PlayerController，用于读 IsPulling")]
    public PlayerController player2Ctrl;

    [Header("配置")]
    [Tooltip("绳索物理参数配置（RopeConfig 资产）")]
    public RopeConfig config;

    [Header("事件")]
    [Tooltip("GameManager 引用，断裂时回调 OnRopeBreak()")]
    public GameManager gameManager;

    [Header("传输特效")]
    [Tooltip("绳子渲染器（拖入 MeshRenderer 或 Obi 渲染器组件）")]
    public Renderer ropeRenderer;
    [Tooltip("传输时绳子的目标颜色")]
    public Color transferColor = new Color(1f, 0.8f, 0.2f, 1f);
    [Tooltip("传输颜色动画时长")]
    public float transferColorDuration = 0.3f;

    // ★ 定长绳长：Start 初始化后不再改变
    float fixedRopeLength;

    // 调参脏标记
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

    void Awake()
    {
        if (rope == null)   rope = GetComponent<ObiRope>();
        if (solver == null) solver = GetComponent<ObiSolver>();
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
            Debug.Log("[RopeController] 绳索未初始化，开始运行时生成...");
            yield return StartCoroutine(rope.GeneratePhysicRepresentationForMesh());
            rope.AddToSolver(null);
            Debug.Log("[RopeController] 绳索初始化完成，粒子数: " + rope.UsedParticles);
        }

        // ★ 太空环境：零重力 + 高刚度 = 绳子不弯不坠，像一根绷紧的太空缆
        if (solver != null)
        {
            solver.parameters.gravity = Vector4.zero;
            // ★ 高迭代保稳：刚度 1.0 需要足够迭代次数才能收敛，避免中间粒子乱跳
            // ★ 迭代不宜过高：刚度 1.0 + 过高迭代 → 数值振荡 → 玩家被微力推着漂
            solver.distanceConstraintParameters.iterations = 10;
            solver.bendingConstraintParameters.iterations = 5;
            solver.UpdateParameters();
            Debug.Log("[RopeController] Obi solver 重力归零, 距离迭代=10, 弯曲迭代=5");
        }

        // 太空零重力 → 绳子不该有任何弯曲/下坠，刚度拉满让它始终笔直
        rope.BendingConstraints.stiffness = 1f;
        rope.DistanceConstraints.stiffness = 1f;
        Debug.Log("[RopeController] 绳索弯曲/拉伸刚度已设为 1.0（太空绷直线）");

        // ★ 缓存 Rigidbody2D（必须在 SetupPins 之前，用于冻结玩家）
        rb1 = player1Collider != null ? player1Collider.GetComponent<Rigidbody2D>() : null;
        rb2 = player2Collider != null ? player2Collider.GetComponent<Rigidbody2D>() : null;

        // ★ 冻结玩家 → Obi pin 约束无法移动 kinematic 物体 → 绳子自己在两人之间就位
        if (rb1 != null) rb1.isKinematic = true;
        if (rb2 != null) rb2.isKinematic = true;

        // === Pin 约束：粒子 0 → P1，粒子 N → P2 ===
        SetupPins();

        // ★ 定长绳长：精确匹配玩家间距，之后永不改变
        float playerDist = (rb1 != null && rb2 != null)
            ? Vector3.Distance(rb1.transform.position, rb2.transform.position)
            : 5f;
        fixedRopeLength = playerDist;

        // ★ 等绳子在冻结的玩家之间自然就位（Obi 需要几帧物理步）
        yield return new WaitForSeconds(0.2f);
        yield return new WaitForFixedUpdate();

        // ★ 解冻玩家，归零残留速度
        if (rb1 != null) { rb1.isKinematic = false; rb1.velocity = Vector2.zero; }
        if (rb2 != null) { rb2.isKinematic = false; rb2.velocity = Vector2.zero; }

        lastUsedParticles = rope.UsedParticles;

        // 强制触发首次参数应用
        lastAppliedBendingStiffness = -1f;
        lastAppliedTearResistance  = -1f;

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
            Debug.Log("[RopeController] Pin: 粒子 0 → Player1");
        }

        // 粒子 N → Player2
        if (player2Collider != null && particleCount > 1)
        {
            batch.AddConstraint(particleCount - 1, player2Collider, pinOffset, Quaternion.identity, 1);
            Debug.Log("[RopeController] Pin: 粒子 " + (particleCount - 1) + " → Player2");
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

        Debug.Log($"[RopeController] {obiCol.name} Obi 碰撞形状已换成微型 (r=0.01)");
    }

    void Update()
    {
        if (rope != null)
        {
            // 检测断裂（UsedParticles 减少说明有粒子被撕裂）
            if (!ropeBroken && rope.UsedParticles < lastUsedParticles && lastUsedParticles > 0)
            {
                ropeBroken = true;
                Debug.Log("[RopeController] 绳子断裂！");
                if (gameManager != null)
                    gameManager.OnRopeBreak();
            }
            lastUsedParticles = rope.UsedParticles;
        }

        // Play Mode 实时调参（只有值变化时才 apply）
        ApplyConfigIfChanged();
    }

    /// <summary>
    /// 弹簧拉力：玩家间距超过定长绳长时双向拉回。
    /// 定长模式下无收绳/放绳，始终双向弹簧。
    /// </summary>
    void FixedUpdate()
    {
        if (rb1 == null || rb2 == null || config == null) return;

        Vector3 p1 = rb1.transform.position;
        Vector3 p2 = rb2.transform.position;
        float dist = Vector3.Distance(p1, p2);

        if (dist > fixedRopeLength)
        {
            Vector3 dir = (p2 - p1).normalized;
            float stretch = dist - fixedRopeLength;
            float force = stretch * config.stretchStiffness * 20f;

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
    /// </summary>
    void LateUpdate()
    {
        if (rope == null || !rope.Initialized || ropeBroken) return;

        var pos = rope.positions;
        var vel = rope.velocities;

        // 粒子 0 → Player1
        if (player1Collider != null && pos.Length > 0)
        {
            Vector3 target = player1Collider.transform.position + pinOffset;
            pos[0] = rope.transform.InverseTransformPoint(target);
            if (vel.Length > 0) vel[0] = Vector3.zero;
        }

        // 粒子 N → Player2
        if (player2Collider != null && pos.Length > 1)
        {
            int idx = rope.UsedParticles - 1;
            if (idx < pos.Length)
            {
                Vector3 target = player2Collider.transform.position + pinOffset;
                pos[idx] = rope.transform.InverseTransformPoint(target);
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

        // --- 绳长：定长模式，Start 初始化后不再改变 ---

        // --- 弯曲刚度 ---
        if (!Mathf.Approximately(config.bendingStiffness, lastAppliedBendingStiffness))
        {
            lastAppliedBendingStiffness = config.bendingStiffness;
            rope.BendingConstraints.stiffness = config.bendingStiffness;
        }

        // ★ 迭代次数已在 Start 硬编码（距离40/弯曲20），不再随刚度动态调整

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
