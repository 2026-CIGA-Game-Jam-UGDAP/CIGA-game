using UnityEngine;

/// <summary>
/// 太空惯性移动 —— 无重力漂浮 + 喷气背包。
/// P1 (playerIndex=0): WASD 四方向喷气
/// P2 (playerIndex=1): 方向键 四方向喷气
///
/// ★ 所有键位集中定义在此，其他脚本直接引用 PlayerController.P1_Snap 等
/// </summary>
public class PlayerController : MonoBehaviour
{
    // ============ 统一键位表 ============
    // P1
    public const KeyCode P1_JetUp      = KeyCode.W;
    public const KeyCode P1_JetLeft    = KeyCode.A;
    public const KeyCode P1_JetRight   = KeyCode.D;
    public const KeyCode P1_Transfer   = KeyCode.E;
    public const KeyCode P1_PullRope   = KeyCode.Q;
    public const KeyCode P1_Snap       = KeyCode.LeftShift;

    // P2
    public const KeyCode P2_JetUp      = KeyCode.UpArrow;
    public const KeyCode P2_JetLeft    = KeyCode.LeftArrow;
    public const KeyCode P2_JetRight   = KeyCode.RightArrow;
    public const KeyCode P2_Transfer   = KeyCode.Keypad0;
    public const KeyCode P2_PullRope   = KeyCode.RightControl;
    public const KeyCode P2_Snap       = KeyCode.Return;

    // 通用
    public const KeyCode Interact          = KeyCode.F;
    public const KeyCode DialogueAdvance   = KeyCode.Space;
    public const KeyCode DialogueAdvanceAlt = KeyCode.F;
    // =====================================
    [Header("玩家索引")]
    [Tooltip("0 = P1 (WASD), 1 = P2 (方向键)")]
    public int playerIndex = 0;

    [Header("移动")]
    [Tooltip("喷气推力大小，值越大加速越快")]
    public float jetForce = 5f;
    [Tooltip("最高移动速度限制")]
    public float maxSpeed = 8f;
    [Tooltip("星球表面移动速度（沿表面切向）")]
    public float moveSpeed = 3f;
    [Tooltip("惯性阻尼。越小越滑（太空漂浮感），0.05=明显漂浮，1=立刻停下")]
    public float linearDrag = 0.05f;

    [Header("能量")]
    [Tooltip("能量上限")]
    [SerializeField] float maxEnergy = 100f;
    [Tooltip("每次喷气瞬时消耗的能量（按一下扣一次）")]
    [SerializeField] float energyPerBurst = 15f;
    float currentEnergy;

    // ★ 瞬时喷气：Update 捕获按下的方向，FixedUpdate 消费后清零
    Vector2 pendingJetImpulse;

    /// <summary>能量百分比 0~1，给 UI Image fill 用</summary>
    public float EnergyPercent => maxEnergy > 0f ? currentEnergy / maxEnergy : 0f;
    /// <summary>当前能量值（给传输系统用）</summary>
    public float CurrentEnergy => currentEnergy;
    /// <summary>最大能量值</summary>
    public float MaxEnergy => maxEnergy;

    /// <summary>是否正在喷气（给 JetParticles 用）</summary>
    public bool IsJetting { get; private set; }

    [Header("队友交互")]
    [Tooltip("拖入另一个玩家的 PlayerController 引用")]
    public PlayerController otherPlayer;
    [Tooltip("按住传输键时每秒传给队友的能量")]
    [SerializeField] float transferRate = 30f;

    /// <summary>是否正在按收绳键（给 RopeController 读）</summary>
    public bool IsPulling { get; private set; }

    [Header("TA 效果")]
    [Tooltip("摄像机抖动组件引用")]
    [SerializeField] CameraShake cameraShake;
    [Tooltip("Sprite 闪白组件引用")]
    [SerializeField] SpriteFlash spriteFlash;
    [Tooltip("受击缩抖组件引用")]
    [SerializeField] DamagePulse damagePulse;
    [Tooltip("冲击线 UI 组件引用")]
    [SerializeField] ImpactLines impactLines;
    [Tooltip("出生弹性动画组件引用")]
    [SerializeField] SpawnBounce spawnBounce;
    [Tooltip("冲击波预制体（运行时 Instantiate）")]
    [SerializeField] GameObject shockwavePrefab;

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Animator animator;
    bool isAnchored;
    bool isFlyingToAnchor;
    AnchorPoint anchoredAt;
    float surfaceT;           // 玩家在表面上的当前距离（沿周长，圆和多边形统一）
    float smoothedAngle;      // 平滑后的当前旋转角度（度），用于 LerpAngle

    // ---- 生命周期 ----

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (rb != null)
        {
            // ★ 太空：无重力 + 低阻尼漂浮
            rb.gravityScale = 0f;
            rb.drag = linearDrag;
            // ★ 自由飞行锁定 Z 旋转，吸附时解锁
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void Start()
    {
        currentEnergy = maxEnergy;

        spawnBounce?.Play();

        Color[] colors = { Color.cyan, Color.red };
        ApplyColor(colors[Mathf.Clamp(playerIndex, 0, colors.Length - 1)]);
    }

    void Update()
    {
        // 初始化/对话中禁用所有输入
        if (GameManager.IsInitializing || DialogueManager.IsActive) return;

        // ★ 瞬时喷气：在 Update 捕获 GetKeyDown，避免 FixedUpdate 丢帧
        pendingJetImpulse += GetJetInputDown();

        if (otherPlayer == null) return;

        // ★ 能量传输：按住给队友传能量（对方满了就停）
        if (Input.GetKey(playerIndex == 0 ? P1_Transfer : P2_Transfer) && currentEnergy > 0f)
        {
            float amount = transferRate * Time.deltaTime;
            amount = Mathf.Min(amount, currentEnergy);
            float space = otherPlayer.MaxEnergy - otherPlayer.CurrentEnergy;
            if (space > 0f)
            {
                amount = Mathf.Min(amount, space);
                currentEnergy -= amount;
                otherPlayer.AddEnergy(amount);
            }
        }

        // ★ 收绳：暴露给 RopeController 读
        IsPulling = Input.GetKey(playerIndex == 0 ? P1_PullRope : P2_PullRope);

        UpdateAnimator();
    }

    void FixedUpdate()
    {
        // 初始化/对话中禁用所有物理移动
        if (GameManager.IsInitializing || DialogueManager.IsActive)
        {
            IsJetting = false;
            return;
        }

        // 飞行中：不处理物理，让协程驱动位置
        if (isFlyingToAnchor)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            IsJetting = false;
            return;
        }

        // ★ 星球表面移动：沿表面切向移动，头朝外、脚朝球心（圆/多边形统一）
        if (isAnchored)
        {
            // 直接读原始水平输入，不用世界空间转换（避免法线方向影响移动）
            float rawHorizontal = 0f;
            if (playerIndex == 0)
            {
                if (Input.GetKey(P1_JetLeft))  rawHorizontal -= 1f;
                if (Input.GetKey(P1_JetRight)) rawHorizontal += 1f;
            }
            else
            {
                if (Input.GetKey(P2_JetLeft))  rawHorizontal -= 1f;
                if (Input.GetKey(P2_JetRight)) rawHorizontal += 1f;
            }

            if (Mathf.Abs(rawHorizontal) > 0.01f)
            {
                // D/→ = 顺时针增加 surfaceT，A/← = 逆时针减少 surfaceT
                surfaceT += rawHorizontal * moveSpeed * Time.fixedDeltaTime;
                IsJetting = true;
            }
            else
            {
                IsJetting = false;
            }

            // ★ 碰碰车弹开：OnCollisionStay2D 里处理，这里不再检测距离

            Vector2 targetPos = anchoredAt.GetSurfacePoint(surfaceT);

            // ★ 统一用预采样的表面法线（圆形：数学公式，多边形：边法线插值）
            Vector2 normal = anchoredAt.GetSurfaceNormal(surfaceT);

            rb.MovePosition(targetPos);
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;

            float targetAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;
            // ★ 平滑旋转过渡（拐角处不会突然跳变）
            smoothedAngle = Mathf.LerpAngle(smoothedAngle, targetAngle, 20f * Time.fixedDeltaTime);
            rb.MoveRotation(smoothedAngle);

            return;
        }

        // ★ 自由飞行：瞬时喷气（按一下 = 一次冲量，不按住持续施力）
        Vector2 input = pendingJetImpulse;
        pendingJetImpulse = Vector2.zero;

        bool jetting = input.sqrMagnitude > 0.01f && currentEnergy >= energyPerBurst;

        if (jetting)
        {
            currentEnergy -= energyPerBurst;
            if (currentEnergy < 0f) currentEnergy = 0f;

            rb.AddForce(input.normalized * jetForce, ForceMode2D.Impulse);
        }

        IsJetting = jetting;

        // ★ 不再硬限速：jetForce 决定每次推力，linearDrag 自然限速。调 jetForce 直观可感
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        bool grounded = isAnchored;
        bool landing = isFlyingToAnchor;
        bool flying = !isAnchored && !isFlyingToAnchor;

        animator.SetBool("IsGrounded", grounded);
        animator.SetBool("IsLanding", landing);
        animator.SetBool("IsFlying", flying);
    }

    // ---- 输入 (Module 2: 四方向喷气) ----

    /// <summary>瞬时喷气输入：按下那一帧才返回方向（星球表面不用这个）</summary>
    Vector2 GetJetInputDown()
    {
        Vector2 raw = Vector2.zero;

        if (playerIndex == 0)
        {
            if (Input.GetKeyDown(P1_JetUp))    raw.y += 1;
            if (Input.GetKeyDown(P1_JetLeft))  raw.x -= 1;
            if (Input.GetKeyDown(P1_JetRight)) raw.x += 1;
        }
        else
        {
            if (Input.GetKeyDown(P2_JetUp))    raw.y += 1;
            if (Input.GetKeyDown(P2_JetLeft))  raw.x -= 1;
            if (Input.GetKeyDown(P2_JetRight)) raw.x += 1;
        }

        return RawToWorldDir(raw);
    }

    /// <summary>把原始输入转为玩家朝向的世界方向：W=朝脸方向(up), A/D=左右</summary>
    Vector2 RawToWorldDir(Vector2 raw)
    {
        Vector2 world = Vector2.zero;
        if (raw.y > 0.01f) world += (Vector2)transform.up;
        if (raw.x > 0.01f) world += (Vector2)transform.right;
        if (raw.x < -0.01f) world -= (Vector2)transform.right;
        return world;
    }

    // ---- Module 4: 锚点吸附（切换行为）----

    /// <summary>锚点调用：平滑飞到球表面最近点，之后在球表面切向移动</summary>
    public void AttachToAnchor(AnchorPoint anchor, float speed)
    {
        if (!isAnchored && !isFlyingToAnchor)
            StartCoroutine(AttachRoutine(anchor, speed));
    }

    /// <summary>锚点调用：解除吸附，恢复自由移动</summary>
    public void DetachFromAnchor()
    {
        isAnchored = false;
        anchoredAt = null;

        // ★ 脱离后重新锁定 Z 旋转，恢复自由飞行姿态
        if (rb != null)
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
    }

    /// <summary>是否已被锚点吸附</summary>
    public bool IsAnchored => isAnchored;
    /// <summary>当前吸附的锚点（null 表示未吸附）</summary>
    public AnchorPoint CurrentAnchor => anchoredAt;
    /// <summary>当前在表面上的位置 t 值</summary>
    public float SurfaceT => surfaceT;

    /// <summary>沿表面推开一段距离（碰碰车弹开用）</summary>
    public void BumpOnSurface(float deltaT)
    {
        if (isAnchored)
            surfaceT += deltaT;
    }

    /// <summary>拾取零件或队友传输时调用</summary>
    public void AddEnergy(float amount)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
    }

    System.Collections.IEnumerator AttachRoutine(AnchorPoint anchor, float speed)
    {
        // ★ 目标：表面上最近点（圆/多边形统一用 GetClosestSurfacePoint）
        Vector3 target = anchor.GetClosestSurfacePoint(transform.position);

        // ★ 提前算好目标法线角度，飞行中同步旋转
        float startAngle = rb != null ? rb.rotation : transform.eulerAngles.z;
        float targetT = anchor.FindClosestSurfaceT(target);
        Vector2 targetNormal = anchor.GetSurfaceNormal(targetT);
        float targetAngle = Mathf.Atan2(targetNormal.y, targetNormal.x) * Mathf.Rad2Deg - 90f;

        // ★ 飞行阶段：不处理物理，协程驱动位置
        isFlyingToAnchor = true;
        if (rb != null) rb.velocity = Vector2.zero;

        Vector3 startPos = transform.position;
        float dist = Vector3.Distance(startPos, target);
        float duration = Mathf.Max(0.2f, dist / speed);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector3 pos = Vector3.Lerp(startPos, target, eased);
            if (rb != null) rb.MovePosition(pos);
            else transform.position = pos;

            // ★ 飞行中同步旋转到目标法线
            float rot = Mathf.LerpAngle(startAngle, targetAngle, eased);
            if (rb != null) rb.MoveRotation(rot);

            yield return null;
        }

        // ★ 到达后进入表面移动模式
        if (rb != null) rb.MovePosition(target);
        else transform.position = target;
        if (rb != null) rb.velocity = Vector2.zero;

        anchoredAt = anchor;

        // ★ 初始化 surfaceT 和旋转：根据到达位置
        // 用 rb.position 而非 transform.position，确保读到物理体已同步的位置
        Vector2 attachPos = rb != null ? rb.position : (Vector2)transform.position;
        surfaceT = anchor.FindClosestSurfaceT(attachPos);
        Vector2 initNormal = anchor.GetSurfaceNormal(surfaceT);
        smoothedAngle = Mathf.Atan2(initNormal.y, initNormal.x) * Mathf.Rad2Deg - 90f;
        // 立即应用旋转，不等下一帧 FixedUpdate
        if (rb != null) rb.MoveRotation(smoothedAngle);

        // ★ 不锁 Z 轴：允许玩家随表面旋转自由翻转
        if (rb != null)
            rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;

        isFlyingToAnchor = false;
        isAnchored = true;
    }

    // ---- 碰碰车弹开（碰撞体检测）----

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isAnchored) return;

        PlayerController other = collision.gameObject.GetComponent<PlayerController>();
        if (other == null || !other.IsAnchored || other.CurrentAnchor != anchoredAt) return;

        // 只让 instanceID 小的一方处理，避免双方重复弹
        if (GetInstanceID() > other.GetInstanceID()) return;

        Vector2 normal = anchoredAt.GetSurfaceNormal(surfaceT);
        Vector2 toOther = (other.transform.position - transform.position).normalized;
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        float dot = Vector2.Dot(toOther, tangent);
        float sign = dot >= 0f ? 1f : -1f;
        float push = 0.4f;
        surfaceT -= sign * push;
        other.BumpOnSurface(sign * push);

        Debug.Log($"[Bumper] ★ 弹开！{name} ↔ {other.name}, push={push:F2}");
        spriteFlash?.Flash();
        cameraShake?.Shake(0.3f);
    }

    // ---- 外观 ----

    void ApplyColor(Color c)
    {
        if (spriteRenderer != null)
            spriteRenderer.color = c;
    }

    // ---- TA 效果（保留接口，直接调用）----

    public void PlayHitFeedback(Vector3 sourcePos, float intensity = 1f)
    {
        Vector3 dir = (sourcePos - transform.position).normalized;
        cameraShake?.Shake(intensity);
        spriteFlash?.Flash();
        damagePulse?.Play();
        impactLines?.Play();
    }

    public void PlayShockwave(Vector3 worldPos)
    {
        if (shockwavePrefab != null)
            Shockwave.Play(shockwavePrefab, worldPos);
    }
}
