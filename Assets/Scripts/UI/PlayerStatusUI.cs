using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CIGA.UI
{
    /// <summary>
    /// 玩家状态 UI——显示单个玩家的生命值、手牌数、死亡状态。
    /// 点击该 UI 会触发目标选择（在 TargetSelector 中处理）。
    /// </summary>
    public class PlayerStatusUI : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI 引用")]
        [Tooltip("玩家名称文本")]
        public TextMeshProUGUI playerNameText;

        [Tooltip("生命值进度条")]
        public Slider hpSlider;

        [Tooltip("生命值文本（当前/最大）")]
        public TextMeshProUGUI hpText;

        [Tooltip("手牌数量文本")]
        public TextMeshProUGUI handCountText;

        [Tooltip("死亡遮罩（玩家死亡时显示）")]
        public GameObject deadOverlay;

        [Tooltip("高亮边框（作为可选目标时）")]
        public Image highlightBorder;

        public void OnPointerClick(PointerEventData eventData)
        {
            throw new System.NotImplementedException();
        }
    }
}