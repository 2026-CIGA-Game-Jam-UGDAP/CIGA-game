using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CIGA.UI
{
    /// <summary>
    /// 卡牌 UI 控件——负责显示单张卡牌的可视化组件。
    /// 单击选中，双击放大预览。
    /// </summary>
    public class CardWidget : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI 引用")]
        [Tooltip("卡面图片")]
        public Image cardImage;

        [Tooltip("卡牌名称文本")]
        public TextMeshProUGUI nameText;

        [Tooltip("费用文本")]
        public TextMeshProUGUI costText;

        [Tooltip("效果描述文本")]
        public TextMeshProUGUI descriptionText;

        [Tooltip("选中高亮边框（默认为隐藏）")]
        public Image highlightBorder;

        [Tooltip("卡牌是否可用的遮罩（不可用时灰色覆盖）")]
        public Image disabledOverlay;

        [Tooltip("是否处于选中状态")]
        public bool IsSelected { get; private set; }

        /// <summary> 单击回调（选中/取消选中）</summary>
        private System.Action<CardWidget> _onClick;

        /// <summary> 双击回调（放大预览）</summary>
        private System.Action<CardWidget> _onDoubleClick;

        /// <summary>
        /// 设置选中状态（高亮边框显示/隐藏）。
        /// </summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (highlightBorder != null)
                highlightBorder.gameObject.SetActive(selected);
        }

        /// <summary>
        /// 设置卡牌是否可用（不可用显示灰色遮罩）。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (disabledOverlay != null)
                disabledOverlay.gameObject.SetActive(!enabled);

            // 不可用时阻止点击
            if (!enabled)
            {
                _onClick = null;
                _onDoubleClick = null;
            }
        }

        // ===== 点击事件 =====

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount == 2)
            {
                // 双击 → 放大预览
                _onDoubleClick?.Invoke(this);
            }
            else if (eventData.clickCount == 1)
            {
                // 单击 → 选中/取消选中
                _onClick?.Invoke(this);
            }
        }
    }
}
