using Mirror;
using UnityEngine;
using System.Collections.Generic;

namespace CIGA.UI
{
    /// <summary>
    /// UI 管理器——客户端的 UI 协调中枢。
    /// 附加在 Player 预制体上，仅本地玩家有权限（OnStartLocalPlayer）。
    /// 负责定时刷新各 UI 组件，并响应 GameManager 的 Rpc 事件。
    /// </summary>
    public class UIManager : NetworkBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("手牌 UI 组件")]
        public HandUI handUI;

        [Tooltip("回合指示器")]
        public TurnIndicatorUI turnIndicator;

        [Tooltip("目标选择器")]
        public TargetSelector targetSelector;

        [Tooltip("所有玩家状态 UI（按玩家索引对应）")]
        public List<PlayerStatusUI> playerStatusUIs;

        [Header("设置")]
        [Tooltip("UI 刷新间隔（秒）")]
        public float refreshInterval = 0.3f;

    }
}