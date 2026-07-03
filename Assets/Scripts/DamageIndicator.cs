using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 方向性受伤指示 — 屏幕边缘弧形红光。
/// 传入世界坐标方向，内部转换到屏幕空间并旋转弧形 UI Image。
/// 挂在 Player 上，Inspector 拖引用弧形 UI Image（Screen Space Overlay Canvas 下的子对象）。
/// </summary>
public class DamageIndicator : MonoBehaviour
{
    [Header("弧形指示器")]
    [Tooltip("弧形红光 UI Image（在 Screen Space Overlay Canvas 下）")]
    [SerializeField] Image arcImage;

    [Header("动画参数")]
    [Tooltip("指示器显示持续时间（秒）")]
    [SerializeField] float showDuration = 0.5f;
    [Tooltip("淡出持续时间（秒）")]
    [SerializeField] float fadeDuration = 0.3f;
    [Tooltip("指示器最大透明度（0~1）")]
    [SerializeField] float maxAlpha = 0.7f;

    Camera mainCam;
    float timer;

    void Awake()
    {
        mainCam = Camera.main;
    }

    /// <summary>显示受伤指示，传入伤害来源的世界坐标方向</summary>
    public void Show(Vector3 worldDirection)
    {
        if (arcImage == null || mainCam == null) return;

        // 世界方向 → 屏幕方向
        // 用伤害方向本身在屏幕上的投影来计算角度
        Vector3 screenDir = mainCam.WorldToScreenPoint(
            transform.position + worldDirection
        ) - mainCam.WorldToScreenPoint(transform.position);

        float angle = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;

        // 旋转弧形 Image：弧形默认朝右(0°)，旋转到对应角度
        arcImage.rectTransform.rotation = Quaternion.Euler(0, 0, angle);

        // 设置透明度并开始淡出
        SetAlpha(maxAlpha);
        timer = showDuration + fadeDuration;

        arcImage.gameObject.SetActive(true);
    }

    void Update()
    {
        if (timer <= 0) return;

        timer -= Time.deltaTime;

        if (timer <= fadeDuration)
        {
            // 淡出阶段
            float alpha = Mathf.Lerp(0, maxAlpha, timer / fadeDuration);
            SetAlpha(alpha);
        }

        if (timer <= 0)
        {
            SetAlpha(0);
            arcImage.gameObject.SetActive(false);
        }
    }

    void SetAlpha(float a)
    {
        if (arcImage == null) return;
        Color c = arcImage.color;
        c.a = a;
        arcImage.color = c;
    }
}
