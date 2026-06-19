using Mirror;
using UnityEngine;

/// <summary>
/// 联机 demo 的玩家控制脚本。
/// 一个文件搞定：移动 + 颜色区分 + 本地 Camera 启用。
/// 拖到 Player prefab 上，配合 NetworkTransform（位置同步）即可。
///
/// 用法：
///   1. 创建胶囊体 prefab，挂 NetworkIdentity + NetworkTransform + 本脚本
///   2. 把 prefab 设为 NetworkManager 的 playerPrefab
///   3. Cinemachine 跟随 Camera 挂在 prefab 子对象上
///   4. OnStartLocalPlayer 会启用本地 Camera、禁用远端 Camera
/// </summary>
public class PlayerController : NetworkBehaviour
{
    [Header("移动")]
    public float moveSpeed = 5f;

    [SyncVar(hook = nameof(OnColorChanged))]
    public Color playerColor = Color.white;

    /// <summary>本地玩家的 Camera（Cinememachine Virtual Camera 或普通 Camera）。</summary>
    [Tooltip("每个玩家自己的 Camera（Cinememachine 或普通 Camera）。OnStartLocalPlayer 会启用本地玩家的、禁用远端的。")]
    public GameObject playerCamera;

    // ---- 移动 ----

    void Update()
    {
        if (!isLocalPlayer) return; // 只有本地玩家才能输入控制

        Vector3 dir = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;

        if (dir != Vector3.zero)
        {
            transform.position += dir * moveSpeed * Time.deltaTime;
        }
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
    }

    // ---- 远端玩家：禁用 Camera ----

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isLocalPlayer && playerCamera != null)
            playerCamera.SetActive(false);
    }
}
