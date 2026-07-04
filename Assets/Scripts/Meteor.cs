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
    [Tooltip("超出视口的比例，0.5 = 半屏外")]
    [SerializeField] float boundsMargin = 1.5f;

    [Header("缩放")]
    [SerializeField] float scaleRandomMin = 0.85f;
    [SerializeField] float scaleRandomMax = 1.15f;

    Camera mainCam;
    bool hasHit;

    void Start()
    {
        mainCam = Camera.main;

        // 随机等比缩放
        float s = Random.Range(scaleRandomMin, scaleRandomMax);
        transform.localScale = Vector3.one * s;
    }

    void Update()
    {
        // 飞行
        transform.Translate(direction.normalized * speed * Time.deltaTime, Space.World);

        // 翻滚
        transform.Rotate(0f, 0f, tumbleSpeed * Time.deltaTime);

        // 飞出屏幕销毁
        if (mainCam != null && IsOutOfBounds())
        {
            Vector3 vp = mainCam.WorldToViewportPoint(transform.position);
            Debug.Log($"[Meteor] 出界销毁 vp=({vp.x:F2},{vp.y:F2}), margin={boundsMargin}");
            Destroy(gameObject);
            return;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 吸附状态：不弹开，陨石直接销毁
        if (player.IsAnchored)
        {
            Debug.Log($"[Meteor] 撞到吸附玩家，销毁");
            Destroy(gameObject);
            return;
        }

        // 非吸附：弹开
        hasHit = true;
        Debug.Log($"[Meteor] 撞到非吸附玩家 {player.name}，弹开");

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
        if (mainCam == null) return false;
        Vector3 vp = mainCam.WorldToViewportPoint(transform.position);
        return vp.x < -boundsMargin || vp.x > 1f + boundsMargin
            || vp.y < -boundsMargin || vp.y > 1f + boundsMargin;
    }
}
