using UnityEngine;

/// <summary>
/// 太空惯性移动 —— 无重力漂浮 + 喷气背包。
/// P1 (playerIndex=0): WASD 四方向喷气
/// P2 (playerIndex=1): 方向键 四方向喷气
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("玩家索引")]
    [Tooltip("0 = P1 (WASD), 1 = P2 (方向键)")]
    public int playerIndex = 0;

    [Header("移动")]
    [Tooltip("喷气推力大小，值越大加速越快")]
    public float jetForce = 5f;
    [Tooltip("最高移动速度限制")]
    public float maxSpeed = 8f;
    [Tooltip("惯性阻尼。越小越滑（太空漂浮感），0.05=明显漂浮，1=立刻停下")]
    public float linearDrag = 0.05f;

    [Header("能量")]
    [Tooltip("能量上限")]
    [SerializeField] float maxEnergy = 100f;
    [Tooltip("喷气每秒消耗的能量")]
    [SerializeField] float drainRate = 25f;
    float currentEnergy;

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
    [Tooltip("拉队友时施加的力，越大拉得越猛")]
    [SerializeField] float pullForce = 4f;
    KeyCode transferKey;
    KeyCode pullKey;

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
    Rigidbody2D otherRb;
    SpriteRenderer spriteRenderer;
    bool isAnchored;
    bool isFlyingToAnchor;
    AnchorPoint anchoredAt;
    float anchorMoveRadius;
    float surfaceAngle; // 玩家在星球表面的当前角度（弧度）

    // ---- 生命周期 ----

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

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

        // 缓存队友 Rigidbody2D
        if (otherPlayer != null)
            otherRb = otherPlayer.GetComponent<Rigidbody2D>();

        // 设置交互按键
        if (playerIndex == 0)
        {
            transferKey = KeyCode.E;
            pullKey = KeyCode.Q;
        }
        else
        {
            transferKey = KeyCode.Keypad0;
            pullKey = KeyCode.RightControl;
        }

        spawnBounce?.Play();

        Color[] colors = { Color.cyan, Color.red };
        ApplyColor(colors[Mathf.Clamp(playerIndex, 0, colors.Length - 1)]);
    }

    void Update()
    {
        if (otherPlayer == null) return;

        // ★ 能量传输：按住给队友传能量（对方满了就停）
        if (Input.GetKey(transferKey) && currentEnergy > 0f)
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

        // ★ 拉队友：按住把队友拉向自己（靠太近就拉不动）
        if (Input.GetKey(pullKey) && otherRb != null)
        {
            float dist = Vector3.Distance(transform.position, otherPlayer.transform.position);
            if (dist > 2.6f) // 最小距离，防止重叠
            {
                Vector3 dir = (transform.position - otherPlayer.transform.position).normalized;
                otherRb.AddForce(dir * pullForce, ForceMode2D.Force);
            }
        }
    }

    void FixedUpdate()
    {
        // 飞行中：不处理物理，让协程驱动位置
        if (isFlyingToAnchor)
        {
            if (rb != null) rb.velocity = Vector2.zero;
            IsJetting = false;
            return;
        }

        // ★ 星球表面移动：沿球表面切向移动，头朝外、脚朝球心
        if (isAnchored)
        {
            Vector2 jetInput = GetJetInput();

            // 仅左右输入有效：左=逆时针，右=顺时针
            float moveDir = 0f;
            if (jetInput.x > 0.01f) moveDir = -1f;
            else if (jetInput.x < -0.01f) moveDir = 1f;

            if (Mathf.Abs(moveDir) > 0.01f)
            {
                float angularSpeed = jetForce / anchorMoveRadius;
                surfaceAngle += moveDir * angularSpeed * Time.fixedDeltaTime;
                IsJetting = true;
            }
            else
            {
                IsJetting = false;
            }

            // 固定在球表面
            Vector2 center = anchoredAt.transform.position;
            Vector2 outward = new Vector2(Mathf.Cos(surfaceAngle), Mathf.Sin(surfaceAngle));
            Vector2 targetPos = center + outward * anchorMoveRadius;

            rb.MovePosition(targetPos);
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // ★ 旋转：头朝外（远离球心），脚朝球心。用 MoveRotation 丝滑
            float targetAngle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg - 90f;
            rb.MoveRotation(targetAngle);

            return;
        }

        Vector2 input = GetJetInput();

        bool jetting = input.sqrMagnitude > 0.01f && currentEnergy > 0f;

        if (jetting)
        {
            float drain = drainRate * Time.fixedDeltaTime;
            currentEnergy -= drain;
            if (currentEnergy < 0f) currentEnergy = 0f;

            rb.AddForce(input.normalized * jetForce, ForceMode2D.Force);
        }

        IsJetting = jetting;

        // 惯性限速（所有方向统一上限）
        rb.velocity = Vector2.ClampMagnitude(rb.velocity, maxSpeed);
    }

    // ---- 输入 (Module 2: 四方向喷气) ----

    Vector2 GetJetInput()
    {
        Vector2 v = Vector2.zero;

        if (playerIndex == 0)
        {
            if (Input.GetKey(KeyCode.W)) v.y += 1;
            // ★ 喷气背包无法向下移动
            if (Input.GetKey(KeyCode.A)) v.x -= 1;
            if (Input.GetKey(KeyCode.D)) v.x += 1;
        }
        else
        {
            if (Input.GetKey(KeyCode.UpArrow))    v.y += 1;
            // ★ 喷气背包无法向下移动
            if (Input.GetKey(KeyCode.LeftArrow))  v.x -= 1;
            if (Input.GetKey(KeyCode.RightArrow)) v.x += 1;
        }

        return v;
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

    /// <summary>拾取零件或队友传输时调用</summary>
    public void AddEnergy(float amount)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
    }

    System.Collections.IEnumerator AttachRoutine(AnchorPoint anchor, float speed)
    {
        float radius = anchor.moveRadius;

        // ★ 目标：球表面最近点（而非球心），避免到达后瞬移
        Vector3 toPlayer = transform.position - anchor.transform.position;
        Vector3 target;
        if (toPlayer.magnitude > 0.001f)
            target = anchor.transform.position + toPlayer.normalized * radius;
        else
            target = anchor.transform.position + Vector3.right * radius;

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
            yield return null;
        }

        // ★ 到达后进入星球表面移动模式
        if (rb != null) rb.MovePosition(target);
        else transform.position = target;
        if (rb != null) rb.velocity = Vector2.zero;

        anchoredAt = anchor;
        anchorMoveRadius = radius;

        // ★ 初始化表面角度：根据玩家在球表面的位置计算
        Vector3 dirFromCenter = transform.position - anchor.transform.position;
        surfaceAngle = Mathf.Atan2(dirFromCenter.y, dirFromCenter.x);

        // ★ 不锁 Z 轴：允许玩家随表面旋转自由翻转
        if (rb != null)
            rb.constraints &= ~RigidbodyConstraints2D.FreezeRotation;

        isFlyingToAnchor = false;
        isAnchored = true;
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
