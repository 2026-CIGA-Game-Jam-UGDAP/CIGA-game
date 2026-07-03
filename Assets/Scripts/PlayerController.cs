using UnityEngine;

/// <summary>
/// 横版跳跃双人本地合作 —— 太空惯性移动。
/// P1 (playerIndex=0): A/D 水平, W/Space 跳跃
/// P2 (playerIndex=1): ← → 水平, ↑/Keypad0 跳跃
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("玩家索引")]
    public int playerIndex = 0; // 0 = WASD, 1 = 方向键

    [Header("移动")]
    public float moveForce = 8f;
    public float maxSpeed = 6f;
    public float jumpForce = 10f;
    [Tooltip("惯性阻尼。越小越滑（太空感），越大越快停。")]
    public float linearDrag = 1.5f;

    [Header("地面检测")]
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.15f;
    public Transform groundCheckPoint;

    [Header("TA 效果")]
    [SerializeField] CameraShake cameraShake;
    [SerializeField] SpriteFlash spriteFlash;
    [SerializeField] DamagePulse damagePulse;
    [SerializeField] ImpactLines impactLines;
    [SerializeField] SpawnBounce spawnBounce;
    [SerializeField] GameObject shockwavePrefab;

    Rigidbody2D rb;
    bool isGrounded;
    SpriteRenderer spriteRenderer;

    // ---- 生命周期 ----

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (rb != null)
            rb.drag = linearDrag;
    }

    void Start()
    {
        spawnBounce?.Play();

        Color[] colors = { Color.cyan, Color.red };
        ApplyColor(colors[Mathf.Clamp(playerIndex, 0, colors.Length - 1)]);
    }

    void Update()
    {
        // 地面检测
        if (groundCheckPoint != null)
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, groundCheckRadius, groundLayer);

        // 跳跃（仅在地面时响应）
        if (IsJumpPressed() && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }

    void FixedUpdate()
    {
        float h = GetHorizontalInput();

        if (Mathf.Abs(h) > 0.01f)
            rb.AddForce(Vector2.right * h * moveForce, ForceMode2D.Force);

        // 限速（只限 X，Y 自由）
        Vector2 vel = rb.velocity;
        vel.x = Mathf.Clamp(vel.x, -maxSpeed, maxSpeed);
        rb.velocity = vel;
    }

    // ---- 输入 ----

    float GetHorizontalInput()
    {
        if (playerIndex == 0)
        {
            if (Input.GetKey(KeyCode.A)) return -1f;
            if (Input.GetKey(KeyCode.D)) return 1f;
        }
        else
        {
            if (Input.GetKey(KeyCode.LeftArrow)) return -1f;
            if (Input.GetKey(KeyCode.RightArrow)) return 1f;
        }
        return 0f;
    }

    bool IsJumpPressed()
    {
        if (playerIndex == 0)
            return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space);
        else
            return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Keypad0);
    }

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
