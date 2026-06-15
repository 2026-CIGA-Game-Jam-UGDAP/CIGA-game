using Mirror;
using UnityEngine;

namespace CIGA.UI
{
    /// <summary>
    /// 目标选择器——管理「选卡→选目标」的核心交互流程。
    /// 点击卡牌选中 → 点击目标玩家立即出牌。
    /// 双击卡牌放大预览。
    /// 仅本地玩家有权限操作，通过 [Command] 发送到服务端。
    /// </summary>
    public class TargetSelector : NetworkBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("手牌 UI 引用")]
        public HandUI handUI;

        [Tooltip("所有玩家状态 UI 的列表")]
        public PlayerStatusUI[] playerStatusUIs;

        [Tooltip("卡牌预览 UI")]
        public CardPreviewUI cardPreview;

        [Tooltip("PASS 按钮")]
        public UnityEngine.UI.Button passButton;

        [Tooltip("提示文本")]
        public TMPro.TextMeshProUGUI promptText;

        [Header("状态")]
        [Tooltip("是否正在选择目标")]
        public bool isSelectingTarget = false;

        [Tooltip("是否是我的回合")]
        public bool isMyTurn = false;

        /// <summary> 当前选中的卡牌 </summary>
        private CardWidget _selectedCard;
    }
}