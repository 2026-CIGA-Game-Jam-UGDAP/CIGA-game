using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕角落能量条。挂到 Fill Image 上，拖入对应玩家。
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
        if (player != null)
            fill.fillAmount = player.EnergyPercent;
    }
}
