using TMPro;
using UnityEngine;

namespace CIGA.UI
{
    /// <summary>
    /// 回合指示器 UI——显示当前是谁的回合、当前是什么阶段。
    /// </summary>
    public class TurnIndicatorUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("回合信息文本（如「玩家1 的回合」）")]
        public TextMeshProUGUI turnText;

        [Tooltip("阶段信息文本（如「出牌阶段」）")]
        public TextMeshProUGUI phaseText;

        /// <summary>
        /// 设置回合信息。
        /// </summary>
        /// <param name="playerName">当前回合玩家名称</param>
        /// <param name="isMyTurn">是否为本地玩家的回合</param>
        public void SetTurnInfo(string playerName, bool isMyTurn = false)
        {
            if (turnText != null)
            {
                if (isMyTurn)
                    turnText.text = $"你的回合";
                else
                    turnText.text = $"{playerName} 的回合";
            }
        }

        /// <summary>
        /// 设置阶段信息。
        /// </summary>
        /// <param name="phaseName">阶段名称（中文）</param>
        public void SetPhaseInfo(string phaseName)
        {
            if (phaseText != null)
                phaseText.text = phaseName;
        }

        /// <summary>
        /// 显示游戏结束信息。
        /// </summary>
        /// <param name="winnerName">获胜者名称</param>
        public void ShowGameOver(string winnerName)
        {
            if (turnText != null)
                turnText.text = $"游戏结束！\n{winnerName} 获胜！";
            if (phaseText != null)
                phaseText.text = "";
        }

        /// <summary>
        /// 重置 UI 到初始状态。
        /// </summary>
        public void ResetDisplay()
        {
            if (turnText != null) turnText.text = "等待游戏开始...";
            if (phaseText != null) phaseText.text = "";
        }
    }
}
