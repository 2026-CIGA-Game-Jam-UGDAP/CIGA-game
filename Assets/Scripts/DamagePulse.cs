using UnityEngine;

/// <summary>
/// 受伤缩抖 — 受击时角色快速膨胀再缩回。
/// 调用 Play() 触发，配合 SpriteFlash 使用效果最佳。
/// 弹性曲线在 Inspector 画，可调出"肉感"或"硬直"不同手感。
/// </summary>
public class DamagePulse : MonoBehaviour
{
    [Header("脉冲参数")]
    [Tooltip("缩放曲线：横轴=时间(0~1)，纵轴=缩放倍率。默认先胀大再缩回")]
    [SerializeField] AnimationCurve pulseCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.1f, 1.25f),
        new Keyframe(0.2f, 0.9f),
        new Keyframe(0.3f, 1.05f),
        new Keyframe(0.4f, 1f)
    );
    [Tooltip("脉冲总持续时间（秒）")]
    [SerializeField] float duration = 0.4f;

    float timer = -1f;
    Vector3 initialScale;

    void Awake()
    {
        initialScale = transform.localScale;
    }

    /// <summary>触发受伤缩抖</summary>
    public void Play()
    {
        timer = 0f;
    }

    /// <summary>指定强度和时长触发</summary>
    public void Play(float intensityMultiplier, float overrideDuration = -1f)
    {
        // intensityMultiplier 目前保留为接口扩展
        if (overrideDuration > 0) duration = overrideDuration;
        timer = 0f;
    }

    void Update()
    {
        if (timer < 0f) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);
        float curve = pulseCurve.Evaluate(t);
        transform.localScale = initialScale * curve;

        if (t >= 1f)
        {
            transform.localScale = initialScale;
            timer = -1f;
        }
    }
}
