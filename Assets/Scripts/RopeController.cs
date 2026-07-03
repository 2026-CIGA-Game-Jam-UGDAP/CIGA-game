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
    public ObiRope rope;
    public ObiSolver solver;
    public ObiRopeCursor cursor;

    [Header("玩家（需要有 ObiCollider 组件）")]
    public ObiColliderBase player1Collider;
    public ObiColliderBase player2Collider;

    [Header("配置")]
    public RopeConfig config;

    [Header("事件")]
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
            batch.AddConstraint(0, player1Collider, Vector3.zero, Quaternion.identity, 1);
            Debug.Log("[RopeController] Pin: 粒子 0 → Player1");
        }

        // 粒子 N → Player2
        if (player2Collider != null && particleCount > 1)
        {
            batch.AddConstraint(particleCount - 1, player2Collider, Vector3.zero, Quaternion.identity, 1);
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
