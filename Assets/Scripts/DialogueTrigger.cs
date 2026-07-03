using UnityEngine;

/// <summary>
/// 挂在 NPC 上。玩家进入触发区域 + 按交互键 → 开始对话。
/// 需要 NPC 有 Collider2D（设 IsTrigger=true）。
/// </summary>
public class DialogueTrigger : MonoBehaviour
{
    [Header("对话数据")]
    [Tooltip("按顺序播放的对话列表")]
    public DialogueSO[] dialogues;

    [Header("提示")]
    [Tooltip("可选的按键提示 UI（如'按 F 对话'气泡），玩家靠近时显示")]
    public GameObject hintUI;

    bool playerInRange;
    int dialogueIndex; // 当前播放到第几段

    void Start()
    {
        if (hintUI != null)
            hintUI.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            if (hintUI != null)
                hintUI.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            if (hintUI != null)
                hintUI.SetActive(false);
        }
    }

    void Update()
    {
        if (!playerInRange) return;
        if (DialogueManager.Instance == null) return;
        if (DialogueManager.IsActive) return;
        if (dialogues == null || dialogues.Length == 0) return;

        if (Input.GetKeyDown(PlayerController.Interact))
        {
            DialogueSO dialogue = dialogues[dialogueIndex % dialogues.Length];
            DialogueManager.Instance.StartDialogue(dialogue);

            // 推进索引：下一段对话（循环）
            dialogueIndex = (dialogueIndex + 1) % dialogues.Length;
        }
    }
}
