using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// 零件收集 HUD。右上角图标+文字「X/N」。
/// GameManager 调 UpdateDisplay（捡零件时带弹跳）、ShakeAndFlashRed（不足时）。
/// </summary>
public class PartCollectionUI : MonoBehaviour
{
    [Header("UI 组件")]
    public TMP_Text countText;
    public RectTransform shakeTarget;

    [Header("收集弹跳（捡到零件）")]
    public float popScale = 1.3f;
    public float popDuration = 0.15f;
    public Color popColor = Color.yellow;

    [Header("失败抖动（零件不足）")]
    public float shakeStrength = 10f;
    public float shakeDuration = 0.3f;
    public int shakeVibrato = 20;
    public Color failFlashColor = Color.red;
    public float failFlashDuration = 0.15f;

    Color originalColor;

    void Start()
    {
        if (countText != null)
            originalColor = countText.color;
        if (shakeTarget == null)
            shakeTarget = GetComponent<RectTransform>();
    }

    /// <summary>更新显示并弹跳：X/N（捡零件时调）</summary>
    public void UpdateDisplay(int collected, int total)
    {
        if (countText != null)
        {
            countText.text = $"{collected}/{total}";

            // 弹跳 + 闪色
            countText.DOKill(true);
            countText.transform.localScale = Vector3.one;
            countText.transform.DOPunchScale(Vector3.one * popScale, popDuration);
            countText.color = popColor;
            countText.DOColor(originalColor, popDuration).SetEase(Ease.OutQuad);
        }
    }

    /// <summary>静默更新显示，不动效（初始化/重置时调）</summary>
    public void SetDisplayQuiet(int collected, int total)
    {
        if (countText != null)
            countText.text = $"{collected}/{total}";
    }

    /// <summary>零件不足时震动+闪红</summary>
    public void ShakeAndFlashRed()
    {
        if (shakeTarget != null)
        {
            shakeTarget.DOKill(true);
            shakeTarget.DOShakePosition(shakeDuration, shakeStrength, shakeVibrato);
        }

        if (countText != null)
        {
            countText.DOKill(true);
            countText.color = failFlashColor;
            countText.DOColor(originalColor, failFlashDuration).SetEase(Ease.OutQuad);
        }
    }
}
