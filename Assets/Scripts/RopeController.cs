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

    [Header("配置")]
    [Tooltip("绳索物理参数配置（RopeConfig 资产）")]
    public RopeConfig config;

    [Header("事件")]
    [Tooltip("GameManager 引用，断裂时回调 OnRopeBreak()")]
    public GameManager gameManager;

    // 调参脏标记：避免每帧 ChangeLength（增删粒子是重操作）
    float lastAppliedLength = -1f;
    float lastAppliedStretchStiffness = -1f;
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
            solver.UpdateParameters(); // ← 关键：推到 Obi 原生库，否则改 struct 不生效
            Debug.Log("[RopeController] Obi solver 重力已归零");
        }

        // 太空零重力 → 绳子不该有任何弯曲/下坠，刚度拉满让它始终笔直
        rope.BendingConstraints.stiffness = 1f;
        rope.DistanceConstraints.stiffness = 1f;
        Debug.Log("[RopeController] 绳索弯曲/拉伸刚度已设为 1.0（太空绷直线）");

        // === Pin 约束：粒子 0 → P1，粒子 N → P2 ===
        SetupPins();

        // 缓存 Rigidbody2D 用于拉力回传
        rb1 = player1Collider != null ? player1Collider.GetComponent<Rigidbody2D>() : null;
        rb2 = player2Collider != null ? player2Collider.GetComponent<Rigidbody2D>() : null;

        lastUsedParticles = rope.UsedParticles;

        // 强制触发首次参数应用
        lastAppliedLength          = -1f;
        lastAppliedStretchStiffness = -1f;
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

        // 2. Play Mode 实时调参（只有值变化时才 apply）
        ApplyConfigIfChanged();
    }

    /// <summary>
    /// 弹簧拉力：Pin 约束是单向的（粒子跟玩家走），绳子张力不会自动回传到玩家。
    /// 这里做一个简化弹簧——玩家间距超过绳长时，互相拉回。
    /// </summary>
    void FixedUpdate()
    {
        if (rb1 == null || rb2 == null || config == null) return;

        Vector3 p1 = rb1.transform.position;
        Vector3 p2 = rb2.transform.position;
        float dist = Vector3.Distance(p1, p2);

        if (dist > config.ropeLength)
        {
            Vector3 dir = (p2 - p1).normalized;
            float stretch = dist - config.ropeLength;
            float force = stretch * config.stretchStiffness * 50f;

            rb1.AddForce(dir * force, ForceMode2D.Force);
            rb2.AddForce(-dir * force, ForceMode2D.Force);
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
}
