using UnityEngine;

/// <summary>
/// 出入口指引 Trigger：在出入口附近放一个 BoxCollider2D (IsTrigger=true)，
/// 玩家走进时通知 GameManager 弹出"同时站在标记区域"提示对话。只触发一次。
/// </summary>
public class ApproachDockTrigger : MonoBehaviour
{
    bool triggered;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;

        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        triggered = true;

        var gm = GameManager.Instance;
        if (gm != null)
            gm.TriggerApproachDockDialogue();
    }
}
