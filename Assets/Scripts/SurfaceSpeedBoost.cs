using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// 拾取后增加玩家在吸附状态（星球表面）的移动速度。
/// 挂在一个带 Collider2D + isTrigger 的 GameObject 上。
/// </summary>
public class SurfaceSpeedBoost : MonoBehaviour
{
    [Header("加速量")]
    [Tooltip("拾取后给玩家 moveSpeed 加的值")]
    public float speedBonus = 3f;
    [Tooltip("加速后 moveSpeed 的最高上限")]
    public float maxSpeedCap = 6f;

    [Header("浮字提示")]
    [Tooltip("飘字初始颜色")]
    public Color popupColor = Color.yellow;
    [Tooltip("飘字字号")]
    public float popupFontSize = 5f;
    [Tooltip("飘字淡出时长（秒）")]
    public float popupDuration = 2f;
    [Tooltip("飘字上飘高度")]
    public float popupFloatHeight = 1f;

    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        pc.moveSpeed = Mathf.Min(pc.moveSpeed + speedBonus, maxSpeedCap);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayEnergyPickup();

        // ★ 浮字提示：在拾取玩家上方生成「移动速度UP！」
        ShowSpeedUpPopup(pc.transform);

        Destroy(gameObject);
    }

    void ShowSpeedUpPopup(Transform playerTransform)
    {
        GameObject popup = new GameObject("SpeedUpPopup");
        popup.transform.position = playerTransform.position + Vector3.up * 2f;

        TextMeshPro tmp = popup.AddComponent<TextMeshPro>();
        tmp.text = "Speed ++！";
        tmp.fontSize = popupFontSize;
        tmp.color = popupColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 100;

        // 描边效果
        tmp.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, 0.8f));
        tmp.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
        tmp.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
        tmp.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
        tmp.fontMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);

        // DoTween：淡出 + 上飘
        tmp.DOFade(0f, popupDuration).SetEase(Ease.OutQuad);
        popup.transform.DOMoveY(popup.transform.position.y + popupFloatHeight, popupDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => Destroy(popup));
    }
}
