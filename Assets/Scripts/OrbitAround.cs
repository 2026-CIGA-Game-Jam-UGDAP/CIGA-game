using UnityEngine;

/// <summary>
/// 挂到需要公转的物体上，该物体将以 center 为圆心做圆周运动。
/// 速度和半径可调。
/// </summary>
public class OrbitAround : MonoBehaviour
{
    [Header("圆心")]
    public GameObject center;

    [Header("参数")]
    public float radius = 3f;
    public float speed = 90f; // 度/秒

    private float currentAngle;

    void Start()
    {
        if (center == null)
        {
            enabled = false;
            return;
        }

        // 根据当前位置初始化角度，避免跳变
        Vector3 offset = transform.position - center.transform.position;
        currentAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }

    void Update()
    {
        if (center == null) return;

        currentAngle += speed * Time.deltaTime;

        float rad = currentAngle * Mathf.Deg2Rad;
        Vector3 pos = center.transform.position + new Vector3(
            Mathf.Cos(rad) * radius,
            Mathf.Sin(rad) * radius,
            0
        );

        transform.position = pos;
    }
}
