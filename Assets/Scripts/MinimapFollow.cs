using UnityEngine;

/// <summary>
/// 小地图相机跟随：挂在 minimap 专用 Camera 上，跟随两玩家中点。
/// 相机需设为 Orthographic，Target Texture 指向一个 RenderTexture。
/// </summary>
public class MinimapFollow : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("玩家 1 的 PlayerController")]
    public PlayerController player1;
    [Tooltip("玩家 2 的 PlayerController")]
    public PlayerController player2;

    [Tooltip("用于限制取景范围的关卡背景")]
    [SerializeField] SpriteRenderer background;

    Camera cam;
    float targetZoom;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) targetZoom = cam.orthographicSize;
    }

    void LateUpdate()
    {
        if (player1 == null || player2 == null || cam == null)
            return;

        Vector3 mid = (player1.transform.position + player2.transform.position) / 2f;
        mid.z = transform.position.z;
        float zoom = targetZoom;
        CameraFollow.KeepViewInsideBackground(background, cam.aspect, ref mid, ref zoom);
        transform.position = mid;
        cam.orthographicSize = zoom;
    }
}
