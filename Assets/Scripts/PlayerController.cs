using UnityEngine;

/// <summary>
/// 太空惯性移动 —— 无重力漂浮 + 喷气背包。
/// P1 (playerIndex=0): WASD 四方向喷气
/// P2 (playerIndex=1): 方向键 四方向喷气
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("玩家索引")]
    public int playerIndex = 0; // 0 = WASD, 1 = 方向键

    [Header("移动")]
    public float jetForce = 5f;
    public float maxSpeed = 8f;
    [Tooltip("惯性阻尼。越小越滑（太空感），0.05=明显漂浮")]
    public float linearDrag = 0.05f;

    [Header("能量")]
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float drainRate = 25f;      // 每秒消耗
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
    public PlayerController otherPlayer;
    [SerializeField] float transferRate = 30f;   // 每秒传输能量
    [SerializeField] float pullForce = 4f;       // 拉队友的力
    KeyCode transferKey;
    KeyCode pullKey;

    [Header("TA 效果")]
    [SerializeField] CameraShake cameraShake;
    [SerializeField] SpriteFlash spriteFlash;
    [SerializeField] DamagePulse damagePulse;
    [SerializeField] ImpactLines impactLines;
    [SerializeField] SpawnBounce spawnBounce;
    [SerializeField] GameObject shockwavePrefab;

    Rigidbody2D rb;
    Rigidbody2D otherRb;
    SpriteRenderer spriteRenderer;
    bool isAnchored;
    bool isFlyingToAnchor;
    AnchorPoint anchoredAt;
    float anchorMoveRadius;

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

        // 已吸附：可在锚点范围内自由移动，不耗能，边界软弹回
        if (isAnchored)
        {
            Vector2 anchoredInput = GetJetInput();
            if (anchoredInput.sqrMagnitude > 0.01f)
            {
                rb.AddForce(anchoredInput.normalized * jetForce, ForceMode2D.Force);
                IsJetting = true;
            }
            else
            {
                IsJetting = false;
            }

            rb.velocity = Vector2.ClampMagnitude(rb.velocity, maxSpeed);

            // 边界约束：超出 moveRadius → 软弹回
            Vector3 toCenter = transform.position - anchoredAt.transform.position;
            float dist = toCenter.magnitude;
            if (dist > anchorMoveRadius && dist > 0.001f)
            {
                Vector2 normal = ((Vector2)toCenter).normalized;
                transform.position = (Vector2)anchoredAt.transform.position + normal * anchorMoveRadius;

                // 反弹：去掉向外的速度分量
                float outward = Vector2.Dot(rb.velocity, normal);
                if (outward > 0f)
                    rb.velocity -= normal * outward * 1.5f;
            }

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
            if (Input.GetKey(KeyCode.S)) v.y -= 1;
            if (Input.GetKey(KeyCode.A)) v.x -= 1;
            if (Input.GetKey(KeyCode.D)) v.x += 1;
        }
        else
        {
            if (Input.GetKey(KeyCode.UpArrow))    v.y += 1;
            if (Input.GetKey(KeyCode.DownArrow))  v.y -= 1;
            if (Input.GetKey(KeyCode.LeftArrow))  v.x -= 1;
            if (Input.GetKey(KeyCode.RightArrow)) v.x += 1;
        }

        return v;
    }

    // ---- Module 4: 锚点吸附（切换行为）----

    /// <summary>锚点调用：平滑飞到锚点中心，之后可在区域内自由移动</summary>
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
        Vector3 target = anchor.transform.position;

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

        // ★ 到达后进入区域移动模式
        if (rb != null) rb.MovePosition(target);
        else transform.position = target;
        if (rb != null) rb.velocity = Vector2.zero;

        anchoredAt = anchor;
        anchorMoveRadius = anchor.moveRadius;

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
