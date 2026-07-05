using UnityEngine;

/// <summary>
/// 飞船对接触发器。挂在 triggerDown / triggerUp 上。
/// 玩家进入时通知 GameManager 检查对接条件，显示等待队友提示。
/// 不锁定玩家移动——玩家可自由进出。
/// </summary>
public class ShipDockTrigger : MonoBehaviour
{
    [Tooltip("场景中的 GameManager")]
    public GameManager gameManager;

    [Tooltip("等待队友提示（世界空间 UI），有玩家时显示")]
    public GameObject waitingText;

    PlayerController dockedPlayer;
    bool shipLaunched;

    public bool HasPlayer => dockedPlayer != null;
    public PlayerController DockedPlayer => dockedPlayer;

    void OnEnable()
    {
        // 修复经典问题：若 collider 在触发器激活时已相交，OnTriggerEnter2D 不会触发
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        var contacts = new Collider2D[8];
        var filter = new ContactFilter2D().NoFilter();
        int count = col.OverlapCollider(filter, contacts);

        for (int i = 0; i < count; i++)
        {
            var pc = contacts[i].GetComponent<PlayerController>();
            if (pc != null)
            {
                dockedPlayer = pc;
                RefreshWaitingText();
                if (gameManager != null)
                    gameManager.OnPlayerDocked();
                break;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (shipLaunched) return; // 只触发一次

        var pc = other.GetComponent<PlayerController>();
        if (pc != null && dockedPlayer == null)
        {
            dockedPlayer = pc;
            RefreshWaitingText();
            if (gameManager != null)
                gameManager.OnPlayerDocked();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (shipLaunched) return; // 只触发一次

        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc == dockedPlayer)
        {
            dockedPlayer = null;
            RefreshWaitingText();
            if (gameManager != null)
                gameManager.OnPlayerUndocked();
        }
    }

    /// <summary>刷新等待队友提示：本 Dock 有玩家时显示</summary>
    void RefreshWaitingText()
    {
        if (waitingText != null)
            waitingText.SetActive(HasPlayer);
    }

    /// <summary>飞船已发射，停止响应进出</summary>
    public void MarkLaunched()
    {
        shipLaunched = true;
        if (waitingText != null)
            waitingText.SetActive(false);
    }
}
