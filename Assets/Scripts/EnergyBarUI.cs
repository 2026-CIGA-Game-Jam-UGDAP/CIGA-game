using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 能量条 UI。单张 Image 用 fillAmount 递减显示。
/// 挂到 Fill Image 上，拖入对应玩家。
/// </summary>
public class EnergyBarUI : MonoBehaviour
{
    [Tooltip("要显示能量的玩家")]
    public PlayerController player;

    Image fill;

    void Start()
    {
        fill = GetComponent<Image>();
    }

    void Update()
    {
        if (player != null && fill != null)
            fill.fillAmount = player.EnergyPercent;
    }
}
