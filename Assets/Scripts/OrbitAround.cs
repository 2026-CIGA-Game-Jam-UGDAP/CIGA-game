using UnityEngine;

/// <summary>
/// 挂到需要公转的物体上，该物体将以 center 为圆心做圆周运动。
/// 速度和半径可调。
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody2D))]
public class OrbitAround : MonoBehaviour
{
    [Header("圆心")]
    public GameObject center;

    [Header("参数")]
    public float radius = 3f;
    public float speed = 90f; // 度/秒

    Rigidbody2D body;
    float currentAngle;

    internal Vector2 StepPosition { get; private set; }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        StepPosition = body.position;
    }

    void Start()
    {
        if (center == null)
        {
            enabled = false;
            return;
        }

        // 根据当前位置初始化角度，避免跳变
        Vector2 offset = body.position - (Vector2)center.transform.position;
        currentAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
    }

    void FixedUpdate()
    {
        if (center == null) return;

        // 星球和吸附玩家在同一物理步使用新的圆心。
        currentAngle = Mathf.Repeat(currentAngle + speed * Time.fixedDeltaTime, 360f);

        float rad = currentAngle * Mathf.Deg2Rad;
        StepPosition = (Vector2)center.transform.position + new Vector2(
            Mathf.Cos(rad) * radius,
            Mathf.Sin(rad) * radius
        );

        body.MovePosition(StepPosition);
    }
}
