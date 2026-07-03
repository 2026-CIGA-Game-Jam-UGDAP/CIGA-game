using UnityEngine;

/// <summary>
/// 双人摄像机：跟随两玩家中点，根据距离自适应 zoom。
/// 挂在 Main Camera 上，拖入两个玩家的 Transform。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("目标")]
    [Tooltip("玩家 1 的 PlayerController")]
    public PlayerController player1;
    [Tooltip("玩家 2 的 PlayerController")]
    public PlayerController player2;

    [Header("参数")]
    [Tooltip("跟随平滑度。越大越跟得紧")]
    public float smoothSpeed = 5f;

    [Tooltip("最小 orthographic size（最近距离）")]
    public float minZoom = 4f;
    [Tooltip("最大 orthographic size（最远距离）")]
    public float maxZoom = 12f;

    [Tooltip("额外边距，防止玩家贴边")]
    public float zoomPadding = 3f;

    Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
    }

    void LateUpdate()
    {
        if (player1 == null || player2 == null || cam == null)
            return;

        // 中点
        Vector3 mid = (player1.transform.position + player2.transform.position) / 2f;
        mid.z = transform.position.z;

        transform.position = Vector3.Lerp(
            transform.position,
            mid,
            smoothSpeed * Time.deltaTime
        );

        // 自适应 zoom：水平距离越大，镜头越远
        float dist = Mathf.Abs(player1.transform.position.x - player2.transform.position.x);
        float targetZoom = dist + zoomPadding;
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoom,
            smoothSpeed * Time.deltaTime
        );
    }
}
