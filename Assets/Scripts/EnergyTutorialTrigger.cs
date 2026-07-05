using UnityEngine;

/// <summary>
/// 能量指引 Trigger：在能量站旁边放一个大 BoxCollider2D (IsTrigger=true)，
/// 玩家走进时通知 GameManager 弹出能量说明对话。只触发一次。
/// </summary>
public class EnergyTutorialTrigger : MonoBehaviour
{
    bool triggered;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;

        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        triggered = true;

        // 通知 GameManager 触发能量教程对话
        var gm = GameManager.Instance;
        if (gm != null)
            gm.TriggerEnergyTutorial();
    }
}
