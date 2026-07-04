using UnityEngine;

/// <summary>
/// 陨石预警 — 在陨石生成位置短暂提示，陨石出现后自毁。
/// </summary>
public class MeteorWarning : MonoBehaviour
{
    [Header("动画")]
    [SerializeField] float duration = 0.6f;
    [SerializeField] AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.2f),
        new Keyframe(0.3f, 1.2f),
        new Keyframe(0.6f, 0.8f),
        new Keyframe(1f, 1f)
    );
    [SerializeField] AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 0.3f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0.5f)
    );

    SpriteRenderer spriteRenderer;
    float elapsed;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        float s = scaleCurve.Evaluate(t);
        transform.localScale = Vector3.one * s;

        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = alphaCurve.Evaluate(t);
            spriteRenderer.color = c;
        }
    }
}
