using UnityEngine;

/// <summary>
/// 飞船对接触发器。挂在 triggerDown / triggerUp 上。
/// 玩家进入时通知 GameManager 检查对接条件。
/// </summary>
public class ShipDockTrigger : MonoBehaviour
{
    [Tooltip("场景中的 GameManager")]
    public GameManager gameManager;

    PlayerController dockedPlayer;

    public bool HasPlayer => dockedPlayer != null;
    public PlayerController DockedPlayer => dockedPlayer;

    void OnEnable()
    {
        // 修复经典问题：若 collider 在触发器激活时已相交，OnTriggerEnter2D 不会触发
        // 在 OnEnable 中主动检测已有重叠的玩家
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
                if (gameManager != null)
                    gameManager.OnPlayerDocked();
                break;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && dockedPlayer == null)
        {
            dockedPlayer = pc;
            if (gameManager != null)
                gameManager.OnPlayerDocked();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null && pc == dockedPlayer)
        {
            dockedPlayer = null;
            if (gameManager != null)
                gameManager.OnPlayerUndocked();
        }
    }
}
