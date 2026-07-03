using UnityEngine;

/// <summary>
/// 终点触发器。挂在终点 GameObject（带 Collider2D + isTrigger）。
/// 每个玩家进入/离开时通知 GameManager。
/// </summary>
public class GoalTrigger : MonoBehaviour
{
    [Tooltip("场景中的 GameManager")]
    public GameManager gameManager;

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null && gameManager != null)
            gameManager.OnPlayerEnterGoal(player.playerIndex);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null && gameManager != null)
            gameManager.OnPlayerExitGoal(player.playerIndex);
    }
}
