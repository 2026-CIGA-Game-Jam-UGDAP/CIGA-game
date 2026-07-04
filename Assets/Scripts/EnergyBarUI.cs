using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 能量条 UI。单张 Image 用 fillAmount 递减显示。
/// 挂到 Fill Image 上，可手动拖入玩家引用，不拖则按 playerIndex 自动查找。
/// </summary>
public class EnergyBarUI : MonoBehaviour
{
    [Tooltip("要显示能量的玩家（不拖则按 playerIndex 自动查找）")]
    public PlayerController player;

    [Tooltip("玩家索引：0=P1, 1=P2")]
    public int playerIndex = 0;

    Image fill;

    void Start()
    {
        fill = GetComponent<Image>();

        if (player == null)
        {
            PlayerController[] players = FindObjectsOfType<PlayerController>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].playerIndex == playerIndex)
                {
                    player = players[i];
                    break;
                }
            }
        }
    }

    void Update()
    {
        if (player != null && fill != null)
            fill.fillAmount = player.EnergyPercent;
    }
}
