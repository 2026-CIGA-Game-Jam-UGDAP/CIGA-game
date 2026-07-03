using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 漫画冲击线 — 受击时屏幕中央闪现放射状冲击线。
/// 需要一个冲击线 Sprite（放射线/锯齿爆裂状），挂在 Player 的 HUD Canvas 下。
/// 每次触发随机旋转角度，配合 DamageIndicator 使用。
/// </summary>
public class ImpactLines : MonoBehaviour
{
    [Header("冲击线 Image")]
    [Tooltip("冲击线 UI Image 组件引用（Screen Space Overlay Canvas 下）")]
    [SerializeField] Image impactImage;

    [Header("动画参数")]
    [Tooltip("缩放弹入阶段持续时间（秒）")]
    [SerializeField] float scaleInDuration = 0.08f;
    [Tooltip("保持阶段持续时间（秒）")]
    [SerializeField] float holdDuration = 0.12f;
    [Tooltip("淡出阶段持续时间（秒）")]
    [SerializeField] float fadeDuration = 0.1f;
    [Tooltip("缩放弹入曲线：横轴=时间(0~1)，纵轴=缩放倍率")]
    [SerializeField] AnimationCurve scaleInCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1.1f),
        new Keyframe(1f, 1f)
    );

    float timer = -1f;
    float totalDuration;
    float targetAngle;
    Vector3 targetScale;

    void Awake()
    {
        if (impactImage != null)
        {
            impactImage.gameObject.SetActive(false);
            targetScale = impactImage.rectTransform.localScale;
            impactImage.rectTransform.localScale = Vector3.zero;
        }
        totalDuration = scaleInDuration + holdDuration + fadeDuration;
    }

    /// <summary>触发冲击线效果</summary>
    public void Play()
    {
        if (impactImage == null) return;

        // 随机旋转 -15°～+15°，让每次冲击线方向不同
        targetAngle = Random.Range(-15f, 15f);
        impactImage.rectTransform.localRotation = Quaternion.Euler(0, 0, targetAngle);

        // 也可以传入方向让冲击线指向伤害来源
        // 这里用随机让每下受击都有点不同

        impactImage.gameObject.SetActive(true);
        impactImage.rectTransform.localScale = Vector3.zero;
        SetAlpha(1f);
        timer = 0f;
    }

    /// <summary>指定角度的冲击线（0=朝右，90=朝上等）</summary>
    public void Play(float angleDegrees)
    {
        if (impactImage == null) return;

        impactImage.rectTransform.localRotation = Quaternion.Euler(0, 0, angleDegrees);
        impactImage.gameObject.SetActive(true);
        impactImage.rectTransform.localScale = Vector3.zero;
        SetAlpha(1f);
        timer = 0f;
    }

    void Update()
    {
        if (timer < 0f) return;

        timer += Time.deltaTime;

        if (timer <= scaleInDuration)
        {
            // 缩放弹入
            float t = timer / scaleInDuration;
            impactImage.rectTransform.localScale = targetScale * scaleInCurve.Evaluate(t);
        }
        else if (timer <= scaleInDuration + holdDuration)
        {
            // 保持
            impactImage.rectTransform.localScale = targetScale;
        }
        else if (timer <= totalDuration)
        {
            // 淡出
            float t = (timer - scaleInDuration - holdDuration) / fadeDuration;
            SetAlpha(1f - t);
            impactImage.rectTransform.localScale = targetScale;
        }
        else
        {
            // 结束
            impactImage.gameObject.SetActive(false);
            timer = -1f;
        }
    }

    void SetAlpha(float a)
    {
        if (impactImage == null) return;
        Color c = impactImage.color;
        c.a = a;
        impactImage.color = c;
    }
}
