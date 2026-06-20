using Mirror;
using UnityEngine;


public class PlayerController : NetworkBehaviour
{
    [Header("移动")]
    public float moveSpeed = 5f;

    [SyncVar(hook = nameof(OnColorChanged))]
    public Color playerColor = Color.white;

    [Tooltip("每个玩家自己的 Camera（子对象即可，自动跟随）。OnStartLocalPlayer 启用本地、禁用远端。")]
    public GameObject playerCamera;

    [Header("动画（可选）")]
    [SerializeField] Animator animator;

    // ---- TA 效果引用 ----

    [Header("TA 效果（仅本地玩家生效）")]
    [SerializeField] CameraShake cameraShake;
    [SerializeField] DamageIndicator damageIndicator;
    [SerializeField] SpriteFlash spriteFlash;
    [SerializeField] DamagePulse damagePulse;
    [SerializeField] ImpactLines impactLines;
    [SerializeField] SpawnBounce spawnBounce;
    [SerializeField] GameObject shockwavePrefab;

    // ---- 移动 ----

    void Update()
    {
        if (!isLocalPlayer) return;

        Vector3 dir = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"),
            0f
        ).normalized;

        bool moving = dir != Vector3.zero;

        if (moving)
        {
            transform.position += dir * moveSpeed * Time.deltaTime;
        }

        // 动画：有 Animator 才设参数，没有就跳过
        if (animator != null)
            animator.SetFloat("Speed", moving ? 1f : 0f);
    }

    // ---- 颜色区分 ----

    /// <summary>服务器端设置颜色：第1个玩家蓝色，第2个红色，以此类推。</summary>
    public override void OnStartServer()
    {
        base.OnStartServer();

        // 根据连接序号分配颜色，简单硬编码（Jam 风格）
        Color[] colors = { Color.cyan, Color.red, Color.green, Color.yellow };
        int index = NetworkServer.connections.Count - 1; // 当前连接数 -1 = 本玩家的序号
        playerColor = colors[Mathf.Clamp(index, 0, colors.Length - 1)];
    }

    void OnColorChanged(Color oldColor, Color newColor)
    {
        // SyncVar hook：颜色变了 → 更新材质
        ApplyColor(newColor);
    }

    void ApplyColor(Color color)
    {
        var renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    // ---- 本地玩家初始化 ----

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // 启用本地玩家的 Camera
        if (playerCamera != null)
            playerCamera.SetActive(true);

        // 确保颜色已应用（本地玩家首次生成时 hook 可能还没触发）
        ApplyColor(playerColor);

        // 弹性出生动画
        if (spawnBounce != null)
            spawnBounce.Play();
    }

    // ---- 远端玩家：禁用 Camera ----

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isLocalPlayer && playerCamera != null)
            playerCamera.SetActive(false);
    }

    // ---- 受击反馈（本地玩家触发，Client-only） ----

    /// <summary>
    /// 本地玩家受伤时调用。传入伤害来源的世界坐标方向。
    /// 纯本地效果：抖屏 + 弧形指示 + 闪白 + 缩抖 + 冲击线。
    /// </summary>
    public void PlayHitFeedback(Vector3 damageSourceDirection, float shakeIntensity = 1f)
    {
        if (!isLocalPlayer) return;

        Vector3 worldDir = (damageSourceDirection - transform.position).normalized;

        if (cameraShake != null) cameraShake.Shake(shakeIntensity);
        if (damageIndicator != null) damageIndicator.Show(worldDir);
        if (spriteFlash != null) spriteFlash.Flash();
        if (damagePulse != null) damagePulse.Play();

        // 冲击线随机旋转（不按方向，每下不一样）
        if (impactLines != null) impactLines.Play();
    }

    /// <summary>
    /// 在指定世界位置播放冲击波。可用于爆炸/大招等。
    /// 纯本地效果，冲击波自毁。
    /// </summary>
    public void PlayShockwave(Vector3 worldPos)
    {
        if (shockwavePrefab == null) return;
        Shockwave.Play(shockwavePrefab, worldPos);
    }
}
