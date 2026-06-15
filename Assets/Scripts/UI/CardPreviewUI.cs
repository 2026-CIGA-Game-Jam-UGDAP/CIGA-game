using UnityEngine;
using UnityEngine.UI;

namespace CIGA.UI
{
    /// <summary>
    /// 卡牌放大预览 UI——双击卡牌时全屏显示。
    /// 复用 CardWidget 展示卡牌信息，不需要额外字段。
    /// </summary>
    public class CardPreviewUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("预览面板根对象（整个弹窗）")]
        public GameObject previewPanel;

        [Tooltip("半透明遮罩背景")]
        public Image backgroundOverlay;

        [Tooltip("大尺寸卡牌控件——复用的 CardWidget")]
        public CardWidget previewCard;

        [Tooltip("关闭按钮")]
        public Button closeButton;

        private void Start()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            if (previewPanel != null)
                previewPanel.SetActive(false);
        }

        /// <summary>
        /// 关闭预览。
        /// </summary>
        public void Hide()
        {
            if (previewPanel != null)
                previewPanel.SetActive(false);
        }
    }
}
