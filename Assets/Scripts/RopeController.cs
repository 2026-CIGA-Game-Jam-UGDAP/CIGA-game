using UnityEngine;
using System.Collections;
using Obi;

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

    // 运行时动态绳长
    float currentRopeLength;
    float lastCursorLength;

    // 调参脏标记：避免每帧 ChangeLength（增删粒子是重操作）
    float lastAppliedBendingStiffness = -1f;
    float lastAppliedTearResistance = -1f;

    bool ropeBroken;
    bool pinsSetup;
    int lastUsedParticles;

    Rigidbody2D rb1;
    Rigidbody2D rb2;

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
            solver.distanceConstraintParameters.iterations = 40;
            solver.bendingConstraintParameters.iterations = 20;
            solver.UpdateParameters(); // ← 关键：推到 Obi 原生库，否则改 struct 不生效
            Debug.Log("[RopeController] Obi solver 重力归零, 距离迭代=40, 弯曲迭代=20");
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

        // 初始化动态绳长：取玩家间距和配置初始绳长的较大值
        float playerDist = (rb1 != null && rb2 != null)
            ? Vector3.Distance(rb1.transform.position, rb2.transform.position)
            : 5f;
        float configLen = config != null ? config.initialRopeLength : 5f;
        currentRopeLength = Mathf.Max(playerDist, configLen);
        lastCursorLength = currentRopeLength;

        // 立刻同步绳长到 Obi
        if (cursor != null)
            cursor.ChangeLength(currentRopeLength);

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
            // 1. 检测断裂（UsedParticles 减少说明有粒子被撕裂）
            if (!ropeBroken && rope.UsedParticles < lastUsedParticles && lastUsedParticles > 0)
            {
                ropeBroken = true;
                Debug.Log("[RopeController] 绳子断裂！");
                if (gameManager != null)
                    gameManager.OnRopeBreak();
            }
            lastUsedParticles = rope.UsedParticles;
        }

        // 2. 动态绳长：收绳 + 放绳
        UpdateRopeLength();

        // 3. Play Mode 实时调参（只有值变化时才 apply）
        ApplyConfigIfChanged();
    }

    /// <summary>
    /// 动态绳长：收绳（按住Q收短）+ 放绳（玩家走远自动伸长）。
    /// </summary>
    void UpdateRopeLength()
    {
        if (config == null) return;
        float maxLen = config.ropeLength;
        float minLen = config.minRopeLength;

        // 统计谁在收绳
        int pullers = 0;
        if (player1Ctrl != null && player1Ctrl.IsPulling) pullers++;
        if (player2Ctrl != null && player2Ctrl.IsPulling) pullers++;

        if (pullers > 0)
        {
            // 收绳：减少绳长
            currentRopeLength -= config.retractionSpeed * pullers * Time.deltaTime;
            if (currentRopeLength < minLen) currentRopeLength = minLen;
        }
        else
        {
            // 放绳：玩家间距 > 当前绳长 → 自动伸长（瞬间，上限 maxLen）
            if (rb1 != null && rb2 != null)
            {
                float dist = Vector3.Distance(rb1.transform.position, rb2.transform.position);
                if (dist > currentRopeLength)
                {
                    currentRopeLength = Mathf.Min(dist, maxLen);
                }
            }
        }

        // 同步到 Obi rope cursor（只在变化足够大时才操作，增删粒子是重操作）
        if (cursor != null && Mathf.Abs(currentRopeLength - lastCursorLength) > 0.1f)
        {
            cursor.ChangeLength(currentRopeLength);
            lastCursorLength = currentRopeLength;
        }
    }

    /// <summary>
    /// 弹簧拉力：玩家间距超过当前绳长时拉回。
    /// 单人收绳 → 单向拖拽（只拉被收的人）；无人收/双人收 → 双向弹簧。
    /// </summary>
    void FixedUpdate()
    {
        if (rb1 == null || rb2 == null || config == null) return;

        Vector3 p1 = rb1.transform.position;
        Vector3 p2 = rb2.transform.position;
        float dist = Vector3.Distance(p1, p2);

        if (dist > currentRopeLength)
        {
            Vector3 dir = (p2 - p1).normalized;        // P1 → P2 方向
            float stretch = dist - currentRopeLength;
            float force = stretch * config.stretchStiffness * 20f;

            bool p1Pulling = player1Ctrl != null && player1Ctrl.IsPulling;
            bool p2Pulling = player2Ctrl != null && player2Ctrl.IsPulling;

            // 只有一个人在收绳 → 单向拖拽（只拉被收的人）
            if (p1Pulling && !p2Pulling)
            {
                // P1 在收 → 把 P2 拉向 P1
                rb2.AddForce(-dir * force, ForceMode2D.Force);
            }
            else if (p2Pulling && !p1Pulling)
            {
                // P2 在收 → 把 P1 拉向 P2
                rb1.AddForce(dir * force, ForceMode2D.Force);
            }
            else
            {
                // 都没收或都在收 → 双向弹簧（保持现有逻辑）
                rb1.AddForce(dir * force, ForceMode2D.Force);
                rb2.AddForce(-dir * force, ForceMode2D.Force);
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

        // --- 绳长：已由 UpdateRopeLength 动态管理，这里不再管 ---

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
}
