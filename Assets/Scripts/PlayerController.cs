using UnityEngine;
using DG.Tweening;

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
    public const KeyCode P1_Transfer   = KeyCode.F;
    public const KeyCode P1_Snap       = KeyCode.LeftShift;

    // P2
    public const KeyCode P2_JetUp      = KeyCode.UpArrow;
    public const KeyCode P2_JetLeft    = KeyCode.LeftArrow;
    public const KeyCode P2_JetRight   = KeyCode.RightArrow;
    public const KeyCode P2_Transfer   = KeyCode.Keypad0;
    public const KeyCode P2_Snap       = KeyCode.Return;

    // 通用
    public const KeyCode Interact          = KeyCode.E;
    public const KeyCode DialogueAdvance   = KeyCode.Space;
    public const KeyCode DialogueAdvanceAlt = KeyCode.E;
    // =====================================
    [Header("玩家索引")]
    [Tooltip("0 = P1 (WASD), 1 = P2 (方向键)")]
    public int playerIndex = 0;

    [Header("移动")]
    [Tooltip("喷气推力大小，值越大加速越快")]
    public float jetForce = 10f;
    [Tooltip("最高移动速度限制")]
    public float maxSpeed = 8f;
    [Tooltip("星球表面移动速度（沿表面切向）")]
    public float moveSpeed = 3f;
    [Tooltip("惯性阻尼。越小越滑（太空漂浮感），0.05=明显漂浮，1=立刻停下")]
    public float linearDrag = 0.05f;
    [Tooltip("吸附后转身角速度（度/秒），值越小转身越平滑")]
    public float turnSpeed = 180f;

    [Header("能量")]
    [Tooltip("能量段数上限")]
    [SerializeField] float maxEnergy = 6f;
    [Tooltip("持续喷气时每秒消耗的能量")]
    [SerializeField] float jetEnergyDrainRate = 2f;
    [Tooltip("最大可传输段数（单次按住上限）")]
    [SerializeField] float maxTransferSegments = 3f;
    float currentEnergy;

    // ★ 持续喷气：FixedUpdate 中直接用 GetKey 读取，每物理帧施加连续推力

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
    [SerializeField] float transferRate = 3f;
    [Tooltip("绳索控制器引用（传输时改变绳子颜色）")]
    [SerializeField] RopeController ropeController;

    /// <summary>当前这次按住已传输的段数（松手清零）</summary>
    float transferredThisHold;

    [Header("音效")]
    [Tooltip("推进器 AudioSource（挂在 Player 上，用于循环喷射音效）")]
    public AudioSource thrusterSource;

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
    [Tooltip("落地烟尘粒子（Player 子对象上的 ParticleSystem）")]
    [SerializeField] ParticleSystem landDust;

    Rigidbody2D rb;
    Animator animator;
    bool isAnchored;
    bool isFlyingToAnchor;
    AnchorPoint anchoredAt;
    PolyAnchorPoint polyAnchoredAt;
    float surfaceT;           // 玩家在表面上的当前距离（沿周长，圆和多边形统一）
    float smoothedAngle;      // 平滑后的当前旋转角度（度），用于 LerpAngle
    bool wasJetting;          // 上一帧是否喷气，用于检测推进器音效 Start/Stop

    // ---- 生命周期 ----

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
    }

    void Update()
    {
        // 初始化/对话中禁用所有输入
        if (GameManager.IsInitializing || DialogueManager.IsActive) return;

        // ★ 持续喷气：FixedUpdate 中直接读取 GetKey，这里不需要额外处理

        if (otherPlayer == null) return;

        // ★ 能量传输：按住给队友传能量（段数制，单次按住有上限）
        KeyCode transferKey = playerIndex == 0 ? P1_Transfer : P2_Transfer;
        bool holdingTransfer = Input.GetKey(transferKey);
        bool transferredThisFrame = false;

        if (holdingTransfer && currentEnergy > 0f && transferredThisHold < maxTransferSegments)
        {
            float amount = transferRate * Time.deltaTime;
            amount = Mathf.Min(amount, currentEnergy);
            amount = Mathf.Min(amount, maxTransferSegments - transferredThisHold);
            float space = otherPlayer.MaxEnergy - otherPlayer.CurrentEnergy;
            if (space > 0f)
            {
                amount = Mathf.Min(amount, space);
                currentEnergy -= amount;
                transferredThisHold += amount;
                otherPlayer.AddEnergy(amount);
                transferredThisFrame = true;
            }
        }

        // 传输特效：只在能量实际流动时变色
        if (ropeController != null)
            ropeController.SetTransferEffect(transferredThisFrame);

        // 松手重置传输计数
        if (!holdingTransfer)
            transferredThisHold = 0f;

        UpdateAnimator();

        // 推进器循环音效：自由飞行喷气时 Start，停止时 Stop
        // 在 Update 而非 FixedUpdate 处理，确保 anchored 状态下也能正确过渡
        bool freeJetting = IsJetting && !isAnchored;
        if (freeJetting && !wasJetting)
        {
            if (AudioManager.Instance != null && thrusterSource != null)
                AudioManager.Instance.StartThruster(transform, thrusterSource);
        }
        else if (!freeJetting && wasJetting)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopThruster(transform);
        }
        wasJetting = freeJetting;
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

            // ★ 用锚点的方向符号：+1 表示 t 增加 = 右（顺时针），-1 表示需要反转
            float moveDir = rawHorizontal * AnchorMoveSign;

            Vector2 currentPos = AnchorSurfacePoint(surfaceT);

            if (Mathf.Abs(moveDir) > 0.01f)
            {
                surfaceT += moveDir * moveSpeed * Time.fixedDeltaTime;
                IsJetting = true;
            }
            else
            {
                IsJetting = false;
            }

            Vector2 targetPos = AnchorSurfacePoint(surfaceT);

            // ★ 绳子约束：只在移动远离队友时限制，同向走不限制
            if (ropeController != null && otherPlayer != null)
            {
                float ropeLen = ropeController.FixedRopeLength;
                if (ropeLen > 0f)
                {
                    Vector2 otherPos = otherPlayer.transform.position;
                    float curDist = Vector2.Distance(currentPos, otherPos);
                    float newDist = Vector2.Distance(targetPos, otherPos);
                    // 只有拉大距离且超出绳长时才 clamp（同向走不会拉大距离）
                    if (newDist > ropeLen && newDist > curDist)
                    {
                        Vector2 dir = (targetPos - otherPos).normalized;
                        Vector2 clampedPos = otherPos + dir * ropeLen;
                        surfaceT = AnchorClosestT(clampedPos);
                        targetPos = AnchorSurfacePoint(surfaceT);
                    }
                }
            }

            Vector2 normal = AnchorSurfaceNormal(surfaceT);
            float targetAngle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg - 90f;

            // ★ 恒速转身：用 MoveTowardsAngle 替代 LerpAngle 指数衰减，
            // 避免突变时首帧转过 40% 的跳变感，匀速旋转更平滑
            smoothedAngle = Mathf.MoveTowardsAngle(smoothedAngle, targetAngle, turnSpeed * Time.fixedDeltaTime);

            // ★ 防御：NaN 不入物理体
            if (!float.IsNaN(targetPos.x) && !float.IsNaN(targetPos.y))
            {
                rb.MovePosition(targetPos);
                rb.velocity = Vector2.zero;
            }
            if (!float.IsNaN(smoothedAngle))
            {
                rb.MoveRotation(smoothedAngle);
            }

            return;
        }

        // ★ 自由飞行：持续喷气（按住 = 持续施力 + 持续消耗能量）
        Vector2 input = GetJetInput();

        bool jetting = input.sqrMagnitude > 0.01f && currentEnergy > 0f;

        if (jetting)
        {
            float drain = jetEnergyDrainRate * Time.fixedDeltaTime;
            currentEnergy -= drain;
            if (currentEnergy < 0f) currentEnergy = 0f;

            rb.AddForce(input.normalized * jetForce, ForceMode2D.Force);
        }

        IsJetting = jetting;

        // ★ 不再硬限速：jetForce 决定推力，linearDrag 自然限速。调 jetForce 直观可感
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        bool grounded = isAnchored;
        bool landing = isFlyingToAnchor;
        bool flying = !isAnchored && !isFlyingToAnchor;

        // 在星球表面左右移动时播放 walk 动画
        bool walking = false;
        if (grounded)
        {
            if (playerIndex == 0)
                walking = Input.GetKey(P1_JetLeft) || Input.GetKey(P1_JetRight);
            else
                walking = Input.GetKey(P2_JetLeft) || Input.GetKey(P2_JetRight);
        }

        animator.SetBool("IsGrounded", grounded);
        animator.SetBool("IsLanding", landing);
        animator.SetBool("IsFlying", flying);
        animator.SetBool("IsWalking", walking);
    }

    // ---- 输入 (Module 2: 四方向喷气) ----

    /// <summary>持续喷气输入：按住期间每帧返回方向（星球表面不用这个）</summary>
    Vector2 GetJetInput()
    {
        Vector2 raw = Vector2.zero;

        if (playerIndex == 0)
        {
            if (Input.GetKey(P1_JetUp))    raw.y += 1;
            if (Input.GetKey(P1_JetLeft))  raw.x -= 1;
            if (Input.GetKey(P1_JetRight)) raw.x += 1;
        }
        else
        {
            if (Input.GetKey(P2_JetUp))    raw.y += 1;
            if (Input.GetKey(P2_JetLeft))  raw.x -= 1;
            if (Input.GetKey(P2_JetRight)) raw.x += 1;
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

    /// <summary>锚点调用：平滑飞到圆表面最近点，之后在表面切向移动</summary>
    public void AttachToAnchor(AnchorPoint anchor, float speed)
    {
        if (!isAnchored && !isFlyingToAnchor)
            StartCoroutine(AttachRoutine(anchor, speed));
    }

    /// <summary>锚点调用：平滑飞到多边形表面最近点，之后在表面切向移动</summary>
    public void AttachToAnchor(PolyAnchorPoint anchor, float speed)
    {
        if (!isAnchored && !isFlyingToAnchor)
            StartCoroutine(AttachRoutine(anchor, speed));
    }

    /// <summary>锚点调用：解除吸附，恢复自由移动</summary>
    public void DetachFromAnchor()
    {
        // ★ 恢复碰撞
        Component anchorComp = (Component)anchoredAt ?? polyAnchoredAt;
        if (anchorComp != null)
        {
            var myCols = GetComponents<Collider2D>();
            foreach (var sc in anchorComp.GetComponents<Collider2D>())
            {
                if (sc.isTrigger) continue;
                foreach (var mc in myCols)
                    Physics2D.IgnoreCollision(mc, sc, false);
            }
        }

        isAnchored = false;
        anchoredAt = null;
        polyAnchoredAt = null;

        // ★ 脱离后重置旋转为 0，重新锁定 Z 旋转，恢复自由飞行姿态
        if (rb != null)
        {
            rb.MoveRotation(0f);
            rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }
    }

    /// <summary>是否已被锚点吸附</summary>
    public bool IsAnchored => isAnchored;
    /// <summary>当前吸附的锚点（用于碰碰车判断是否同锚点，返回 object 做引用比较）</summary>
    public object CurrentAnchor => (object)anchoredAt ?? polyAnchoredAt;
    /// <summary>当前在表面上的位置 t 值</summary>
    public float SurfaceT => surfaceT;

    // ---- 表面接口分发（AnchorPoint 和 PolyAnchorPoint 方法名一致，手动分发） ----
    float AnchorSurfaceLength => polyAnchoredAt != null ? polyAnchoredAt.SurfaceLength : (anchoredAt != null ? anchoredAt.SurfaceLength : 0f);
    float AnchorMoveSign => polyAnchoredAt != null ? polyAnchoredAt.MoveDirectionSign : (anchoredAt != null ? anchoredAt.MoveDirectionSign : -1f);
    Vector2 AnchorSurfacePoint(float t) => polyAnchoredAt != null ? polyAnchoredAt.GetSurfacePoint(t) : anchoredAt.GetSurfacePoint(t);
    Vector2 AnchorSurfaceNormal(float t) => polyAnchoredAt != null ? polyAnchoredAt.GetSurfaceNormal(t) : anchoredAt.GetSurfaceNormal(t);
    float AnchorClosestT(Vector3 pos) => polyAnchoredAt != null ? polyAnchoredAt.FindClosestSurfaceT(pos) : anchoredAt.FindClosestSurfaceT(pos);
    Vector2 AnchorClosestPoint(Vector3 pos) => polyAnchoredAt != null ? polyAnchoredAt.GetClosestSurfacePoint(pos) : anchoredAt.GetClosestSurfacePoint(pos);

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
        // ★ 防御：speed 无效则直接吸附到位
        if (speed <= 0f || float.IsNaN(speed))
            speed = 9999f;

        // ★ 目标：表面上最近点（圆/多边形统一用 GetClosestSurfacePoint）
        Vector3 target = anchor.GetClosestSurfacePoint(transform.position);

        // ★ 提前算好目标法线角度，飞行中同步旋转
        float startAngle = rb != null ? rb.rotation : transform.eulerAngles.z;
        float targetT = anchor.FindClosestSurfaceT(target);
        Vector2 targetNormal = anchor.GetSurfaceNormal(targetT);
        float targetAngle = Mathf.Atan2(targetNormal.y, targetNormal.x) * Mathf.Rad2Deg - 90f;

        // ★ 防御：NaN 检查（当 anchor 半径为 0 时 surface 方法可能返回 NaN）
        if (float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsNaN(startAngle) || float.IsNaN(targetAngle))
        {
            isFlyingToAnchor = false;
            yield break;
        }

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

            // ★ 防御：NaN 不入物理体
            if (!float.IsNaN(pos.x) && !float.IsNaN(pos.y))
            {
                if (rb != null) rb.MovePosition(pos);
                else transform.position = pos;
            }

            // ★ 飞行中同步旋转到目标法线
            float rot = Mathf.LerpAngle(startAngle, targetAngle, eased);
            if (!float.IsNaN(rot))
            {
                if (rb != null) rb.MoveRotation(rot);
            }

            yield return null;
        }

        // ★ 到达后进入表面移动模式
        if (!float.IsNaN(target.x) && !float.IsNaN(target.y))
        {
            if (rb != null) rb.MovePosition(target);
            else transform.position = target;
        }
        if (rb != null) rb.velocity = Vector2.zero;

        anchoredAt = anchor;

        // ★ 初始化 surfaceT 和旋转：根据到达位置
        // 用 rb.position 而非 transform.position，确保读到物理体已同步的位置
        Vector2 attachPos = rb != null ? rb.position : (Vector2)transform.position;
        surfaceT = anchor.FindClosestSurfaceT(attachPos);
        Vector2 initNormal = anchor.GetSurfaceNormal(surfaceT);
        smoothedAngle = Mathf.Atan2(initNormal.y, initNormal.x) * Mathf.Rad2Deg - 90f;
        // 立即应用旋转，不等下一帧 FixedUpdate
        if (!float.IsNaN(smoothedAngle) && rb != null) rb.MoveRotation(smoothedAngle);

        // ★ 不锁 Z 轴：允许玩家随表面旋转自由翻转
        if (rb != null)
            rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;

        isFlyingToAnchor = false;
        isAnchored = true;

        // ★ 忽略碰撞
        IgnoreSurfaceCollision(anchor, true);

        // 落地反馈
        landDust?.Play();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAdsorption(transform.position);
        cameraShake?.Shake(0.5f);
    }

    System.Collections.IEnumerator AttachRoutine(PolyAnchorPoint anchor, float speed)
    {
        // ★ 防御：speed 无效则直接吸附到位
        if (speed <= 0f || float.IsNaN(speed))
            speed = 9999f;

        Vector3 target = anchor.GetClosestSurfacePoint(transform.position);

        float startAngle = rb != null ? rb.rotation : transform.eulerAngles.z;
        float targetT = anchor.FindClosestSurfaceT(target);
        Vector2 targetNormal = anchor.GetSurfaceNormal(targetT);
        float targetAngle = Mathf.Atan2(targetNormal.y, targetNormal.x) * Mathf.Rad2Deg - 90f;

        // ★ 防御：NaN 检查
        if (float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsNaN(startAngle) || float.IsNaN(targetAngle))
        {
            isFlyingToAnchor = false;
            yield break;
        }

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

            if (!float.IsNaN(pos.x) && !float.IsNaN(pos.y))
            {
                if (rb != null) rb.MovePosition(pos);
                else transform.position = pos;
            }

            float rot = Mathf.LerpAngle(startAngle, targetAngle, eased);
            if (!float.IsNaN(rot))
            {
                if (rb != null) rb.MoveRotation(rot);
            }

            yield return null;
        }

        if (!float.IsNaN(target.x) && !float.IsNaN(target.y))
        {
            if (rb != null) rb.MovePosition(target);
            else transform.position = target;
        }
        if (rb != null) rb.velocity = Vector2.zero;

        polyAnchoredAt = anchor;

        Vector2 attachPos = rb != null ? rb.position : (Vector2)transform.position;
        surfaceT = anchor.FindClosestSurfaceT(attachPos);
        Vector2 initNormal = anchor.GetSurfaceNormal(surfaceT);
        smoothedAngle = Mathf.Atan2(initNormal.y, initNormal.x) * Mathf.Rad2Deg - 90f;
        if (!float.IsNaN(smoothedAngle) && rb != null) rb.MoveRotation(smoothedAngle);

        if (rb != null)
            rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;

        isFlyingToAnchor = false;
        isAnchored = true;

        // ★ 忽略玩家与表面非 trigger collider 的物理碰撞，防止物理引擎推歪/推倒玩家
        IgnoreSurfaceCollision(anchor, true);

        landDust?.Play();
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAdsorption(transform.position);
        cameraShake?.Shake(0.5f);
    }

    // ---- 碰碰车弹开 ----

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isAnchored) return;

        PlayerController other = collision.gameObject.GetComponent<PlayerController>();
        object myAnchor = (object)anchoredAt ?? polyAnchoredAt;
        if (other == null || !other.IsAnchored || other.CurrentAnchor != myAnchor) return;

        // 只让 instanceID 小的一方处理，避免双方重复
        if (GetInstanceID() > other.GetInstanceID()) return;

        Vector2 normal = AnchorSurfaceNormal(surfaceT);
        Vector2 toOther = (other.transform.position - transform.position).normalized;
        Vector2 tangent = new Vector2(-normal.y, normal.x);
        float dot = Vector2.Dot(toOther, tangent);
        float sign = dot >= 0f ? 1f : -1f;
        float push = 0.4f;
        surfaceT -= sign * push;
        other.BumpOnSurface(sign * push);

        spriteFlash?.Flash();
        cameraShake?.Shake(0.3f);
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

    /// ★ 忽略/恢复玩家与锚点表面的物理碰撞
    void IgnoreSurfaceCollision(Component anchor, bool ignore)
    {
        var myCols = GetComponents<Collider2D>();
        foreach (var sc in anchor.GetComponents<Collider2D>())
        {
            if (sc.isTrigger) continue;
            foreach (var mc in myCols)
                Physics2D.IgnoreCollision(mc, sc, ignore);
        }
    }

    // ---- 音效接力（Animation Event / 其他脚本调用） ----

    /// <summary>
    /// Animation Event 接力：走路动画关键帧调用此方法。
    /// 根据锚点类型自动区分月球脚步 vs 火箭脚步。
    /// </summary>
    public void OnFootstep()
    {
        if (AudioManager.Instance == null) return;

        // 圆形锚点 → 月球脚步；多边形锚点 → 火箭脚步
        bool isMoon = anchoredAt != null;
        AudioManager.Instance.PlayFootstepFromPlayer(transform, isMoon);
    }
}
