using UnityEngine;

/// <summary>
/// 地面冲击波 — Sprite 圆环放大 + 淡出。
/// 用法：在爆炸/大招位置 Instantiate shockwavePrefab，脚本自动播放扩散动画后自毁。
/// 参数全在 Inspector 调。
/// </summary>
public class Shockwave : MonoBehaviour
{
    [Header("扩散参数")]
    [SerializeField] float startRadius = 0.2f;
    [SerializeField] float targetRadius = 5f;
    [SerializeField] float expandDuration = 0.4f;
    [SerializeField] float fadeDuration = 0.3f;

    SpriteRenderer sr;
    float timer;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>在指定世界位置播放冲击波</summary>
    public static Shockwave Play(GameObject prefab, Vector3 worldPos)
    {
        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity);
        Shockwave sw = obj.GetComponent<Shockwave>();
        if (sw == null) sw = obj.AddComponent<Shockwave>();
        return sw;
    }

    void Start()
    {
        // 初始缩放
        SetRadius(startRadius);
        SetAlpha(1f);
        timer = 0f;
    }

    void Update()
    {
        timer += Time.deltaTime;

        float totalDuration = expandDuration + fadeDuration;

        if (timer <= expandDuration)
        {
            // 扩散阶段：从 startRadius → targetRadius
            float t = timer / expandDuration;
            float radius = Mathf.Lerp(startRadius, targetRadius, t);
            SetRadius(radius);
            SetAlpha(1f);
        }
        else if (timer <= totalDuration)
        {
            // 淡出阶段：半径保持 targetRadius，透明度 → 0
            SetRadius(targetRadius);
            float fadeT = (timer - expandDuration) / fadeDuration;
            SetAlpha(1f - fadeT);
        }
        else
        {
            // 动画结束，自毁
            Destroy(gameObject);
        }
    }

    void SetRadius(float radius)
    {
        // 俯视角 2D：圆环 Sprite，缩放 = radius / sprite原始尺寸
        // 简化处理：直接用 localScale = radius（假设 Sprite 在 1x1 单位内）
        transform.localScale = new Vector3(radius, radius, 1f);
    }

    void SetAlpha(float alpha)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = alpha;
        sr.color = c;
    }
}
