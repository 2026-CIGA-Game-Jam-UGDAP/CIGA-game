using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace CIGA.UI
{
    /// <summary>
    /// 手牌 UI——管理手牌中所有卡牌的显示。
    /// 每帧响应 SyncList 的变化，刷新卡牌控件列表。
    /// </summary>
    public class HandUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("卡牌控件预制体")]
        public GameObject cardWidgetPrefab;

        [Tooltip("手牌容器（通常是 Horizontal Layout Group）")]
        public Transform handContainer;

        /// <summary> 当前显示的卡牌控件列表 </summary>
        private List<CardWidget> _cards = new List<CardWidget>();

        /// <summary> 当前选中的卡牌 </summary>
        private CardWidget _selectedCard;

        /// <summary>
        /// 清除选中状态。
        /// </summary>
        public void ClearSelection()
        {
            if (_selectedCard != null)
                _selectedCard.SetSelected(false);
            _selectedCard = null;
        }

        /// <summary>
        /// 获取当前选中的卡牌控件。
        /// </summary>
        public CardWidget GetSelected() => _selectedCard;

        /// <summary>
        /// 选中一张卡牌（会自动取消之前的选中）。
        /// </summary>
        public void SelectCard(CardWidget widget)
        {
            ClearSelection();
            _selectedCard = widget;
            _selectedCard?.SetSelected(true);
        }
    }
}
