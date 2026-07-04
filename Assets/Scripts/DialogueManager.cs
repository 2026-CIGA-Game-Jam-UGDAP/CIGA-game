using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Febucci.UI;
using DG.Tweening;

/// <summary>
/// 对话系统全局单例。挂在场景 Canvas 上。
/// 负责：显示/隐藏对话面板、逐行播放对话、打字机效果、立绘动画。
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    /// <summary>对话是否正在播放。其他脚本（如 PlayerController）读这个来禁用输入。</summary>
    public static bool IsActive => Instance != null && Instance.dialoguePanel.activeSelf;

    [Header("UI 引用")]
    [Tooltip("对话面板根节点（包含所有对话 UI）")]
    public GameObject dialoguePanel;

    [Tooltip("左侧立绘 Image")]
    public Image leftPortrait;
    [Tooltip("右侧立绘 Image")]
    public Image rightPortrait;
    [Tooltip("左侧立绘父节点（用于 SetActive 开关）")]
    public GameObject leftPortraitGo;
    [Tooltip("右侧立绘父节点（用于 SetActive 开关）")]
    public GameObject rightPortraitGo;

    [Tooltip("说话人名字 Text (TMP)")]
    public TMP_Text speakerNameText;
    [Tooltip("对话内容 Text (TMP)")]
    public TMP_Text dialogueText;

    [Header("TextAnimator（迭代：打字机效果）")]
    [Tooltip("对话 Text 上的 TextAnimator_TMP 组件")]
    public TextAnimator_TMP textAnimator;
    [Tooltip("TextAnimator 上的 TypewriterByCharacter 组件")]
    public TypewriterByCharacter typewriter;

    [Header("提示")]
    [Tooltip("'按 F 继续' 提示")]
    public GameObject pressHint;

    [Header("动画参数")]
    [Tooltip("立绘淡入淡出时长（秒）")]
    public float portraitFadeDuration = 0.3f;

    [Header("输入")]
    [Tooltip("推进对话按键 — 见 PlayerController.DialogueAdvance / DialogueAdvanceAlt")]

    // 内部状态
    DialogueSO currentDialogue;
    int currentLineIndex;
    bool lineFullyShown; // 当前行打字机是否播完

    void Awake()
    {
        Instance = this;
        dialoguePanel.SetActive(false);
    }

    /// <summary>DialogueTrigger 调用，开始一段对话</summary>
    public void StartDialogue(DialogueSO dialogue)
    {
        if (dialogue == null || dialogue.lines.Length == 0)
            return;

        currentDialogue = dialogue;
        currentLineIndex = 0;

        dialoguePanel.SetActive(true);
        Time.timeScale = 0f;

        ShowLine(currentDialogue.lines[0]);
    }

    void Update()
    {
        if (!IsActive) return;

        if (Input.GetKeyDown(PlayerController.DialogueAdvance) || Input.GetKeyDown(PlayerController.DialogueAdvanceAlt))
        {
            if (!lineFullyShown)
            {
                // 打字中 → 直接完成当前句
                CompleteTyping();
            }
            else
            {
                // 已播完 → 下一句
                AdvanceDialogue();
            }
        }
    }

    /// <summary>显示一行对话</summary>
    void ShowLine(DialogueLine line)
    {
        lineFullyShown = false;

        // 立绘
        UpdatePortraits(line);

        // 名字
        speakerNameText.text = line.speakerName;

        // 文本
        if (currentDialogue.useTypingEffect && typewriter != null)
        {
            // ★ 迭代功能：TextAnimator 打字机
            typewriter.ShowText(line.text);
            typewriter.onTextShowed.RemoveAllListeners();
            typewriter.onTextShowed.AddListener(() => OnTypingComplete());
        }
        else
        {
            // 直接显示全文
            dialogueText.text = line.text;
            OnTypingComplete();
        }

        pressHint.SetActive(false);
    }

    /// <summary>打字机完成回调</summary>
    void OnTypingComplete()
    {
        lineFullyShown = true;
        pressHint.SetActive(true);
    }

    /// <summary>跳过当前打字机，立刻显示完整文本</summary>
    void CompleteTyping()
    {
        if (typewriter != null && typewriter.isShowingText)
        {
            typewriter.SkipTypewriter();
        }
        else
        {
            // 没有打字机组件：直接标记完成
            dialogueText.text = currentDialogue.lines[currentLineIndex].text;
            OnTypingComplete();
        }
    }

    /// <summary>播放下一行，或结束对话</summary>
    void AdvanceDialogue()
    {
        currentLineIndex++;

        if (currentLineIndex >= currentDialogue.lines.Length)
        {
            EndDialogue();
            return;
        }

        ShowLine(currentDialogue.lines[currentLineIndex]);
    }

    /// <summary>结束对话</summary>
    void EndDialogue()
    {
        // 先停掉正在打的字
        if (typewriter != null && typewriter.isShowingText)
            typewriter.SkipTypewriter();

        dialoguePanel.SetActive(false);

        // 立绘复位
        leftPortraitGo.SetActive(false);
        rightPortraitGo.SetActive(false);

        Time.timeScale = 1f;
        currentDialogue = null;
    }

    /// <summary>更新左右立绘显示</summary>
    void UpdatePortraits(DialogueLine line)
    {
        Image activePortrait = line.isLeftSide ? leftPortrait : rightPortrait;
        Image inactivePortrait = line.isLeftSide ? rightPortrait : leftPortrait;
        GameObject activeGo = line.isLeftSide ? leftPortraitGo : rightPortraitGo;
        GameObject inactiveGo = line.isLeftSide ? rightPortraitGo : leftPortraitGo;

        // 非说话方：隐藏或变暗
        if (inactiveGo.activeSelf && currentDialogue.useAnimation)
        {
            inactivePortrait.DOFade(0.3f, portraitFadeDuration).SetUpdate(true);
        }

        // 说话方
        activeGo.SetActive(true);
        activePortrait.sprite = line.portrait;

        if (currentDialogue.useAnimation)
        {
            // ★ 迭代功能：DOTween 立绘淡入
            activePortrait.color = new Color(1, 1, 1, 0);
            activePortrait.DOFade(1f, portraitFadeDuration).SetUpdate(true);
        }
        else
        {
            activePortrait.color = Color.white;
        }
    }

    void OnDestroy()
    {
        // 清理：如果对话中销毁，恢复时停
        if (IsActive)
            Time.timeScale = 1f;
    }
}
