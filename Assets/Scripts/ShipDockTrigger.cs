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
