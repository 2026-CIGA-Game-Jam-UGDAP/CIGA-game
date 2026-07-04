using UnityEngine;

/// <summary>
/// 陨石 — 从右上往左下飞，Trigger 碰玩家。
/// 非吸附玩家被砸中弹飞，吸附玩家无视。
/// </summary>
public class Meteor : MonoBehaviour
{
    [Header("运行时设置（由 MeteorManager 注入）")]
    public Vector2 direction = Vector2.down + Vector2.left;
    public float speed = 8f;
    public float knockbackForce = 80f;

    [Header("视觉")]
    [SerializeField] float tumbleSpeed = 360f;

    [Header("边界销毁")]
    [SerializeField] float boundsMargin = 5f;

    Camera mainCam;
    bool hasHit;

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        // 飞行
        transform.Translate(direction.normalized * speed * Time.deltaTime, Space.World);

        // 翻滚
        transform.Rotate(0f, 0f, tumbleSpeed * Time.deltaTime);

        // 飞出屏幕销毁
        if (mainCam != null && IsOutOfBounds())
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 吸附状态：不弹开，陨石直接销毁
        if (player.IsAnchored)
        {
            Destroy(gameObject);
            return;
        }

        // 非吸附：弹开
        hasHit = true;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 knockDir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            rb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
        }

        // TA 反馈
        player.PlayHitFeedback(transform.position, 1f);

        Destroy(gameObject);
    }

    bool IsOutOfBounds()
    {
        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        return viewportPos.x < -0.1f || viewportPos.x > 1.1f
            || viewportPos.y < -0.5f || viewportPos.y > 1.5f; // 下方给更多容错
    }
}
