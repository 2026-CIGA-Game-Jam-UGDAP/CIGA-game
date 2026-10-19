using UnityEngine;

/// <summary>
/// 陨石 — 从右上往左下飞，Trigger 碰玩家。
/// 命中时陨石碎裂；非吸附玩家被砸中弹飞，吸附玩家保持原位。
/// </summary>
public class Meteor : MonoBehaviour
{
    [Header("运行时设置（由 MeteorManager 注入）")]
    public Vector2 direction = Vector2.down + Vector2.left;
    public float speed = 8f;
    public float knockbackForce = 80f;

    [Header("视觉")]
    [SerializeField] float tumbleSpeed = 360f;

    [Header("命中反馈")]
    [Tooltip("独立播放的陨石碎裂特效，不随陨石一起销毁")]
    [SerializeField] GameObject impactPrefab;

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
            Destroy(gameObject);
            return;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null) return;

        // 两种状态都播放碎裂；先标记，避免同帧多个玩家碰撞体重复触发。
        hasHit = true;
        PlayImpact();
        Destroy(gameObject);

        // 吸附状态：只碎裂，不弹开或播放玩家受伤反馈。
        if (player.IsAnchored)
            return;

        // 非吸附：弹开
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            Vector2 knockDir = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            rb.AddForce(knockDir * knockbackForce, ForceMode2D.Impulse);
        }

        // TA 反馈
        player.PlayHitFeedback(transform.position, 1f);

        // 全屏 Bloom 爆发
        PostProcessController.Flash(4f, 0.15f);
    }

    void PlayImpact()
    {
        if (impactPrefab == null) return;

        var spriteRenderer = GetComponent<SpriteRenderer>();
        Vector3 position = transform.position;
        float size = 1f;
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // 原陨石 Sprite 的轴心在角落，按画面中心和原始尺寸对齐碎裂。
            position = spriteRenderer.bounds.center;
            Vector3 scaledSize = Vector3.Scale(spriteRenderer.sprite.bounds.size, transform.lossyScale);
            size = Mathf.Max(Mathf.Abs(scaledSize.x), Mathf.Abs(scaledSize.y));
        }

        GameObject impact = Instantiate(impactPrefab, position, Quaternion.identity);
        impact.transform.localScale = Vector3.one * size;
    }

    bool IsOutOfBounds()
    {
        if (mainCam == null) return false;
        Vector3 vp = mainCam.WorldToViewportPoint(transform.position);
        return vp.x < -boundsMargin || vp.x > 1f + boundsMargin
            || vp.y < -boundsMargin || vp.y > 1f + boundsMargin;
    }
}
