using UnityEngine;

/// <summary>
/// 挂在可交互对象上（NPC、终点等）。玩家进入触发范围 → 显示头顶图标，离开 → 隐藏。
/// 纯检测层，按 E 后的具体行为由 DialogueTrigger / GoalTrigger 等各自处理。
/// 需要 Collider2D + IsTrigger。
/// </summary>
public class Interactable : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
            pc.ShowInteractPrompt();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc != null)
            pc.HideInteractPrompt();
    }
}
