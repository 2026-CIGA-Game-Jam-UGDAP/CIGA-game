using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Febucci.UI;
using DG.Tweening;

/// <summary>
/// 对话系统全局单例。挂在场景 Canvas 上。
/// 布局：中央大立绘 + 左右小头像组（头像+固定name）+ 底部对话框。
/// 旁白时全部隐藏，只留居中文本。
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    /// <summary>对话是否正在播放。其他脚本（如 PlayerController）读这个来禁用输入。</summary>
    public static bool IsActive => Instance != null && Instance.dialoguePanel.activeSelf;

    [Header("面板根节点")]
    [Tooltip("对话面板根节点（包含所有对话 UI）")]
    public GameObject dialoguePanel;

    [Header("中央大立绘")]
    [Tooltip("屏幕中央大立绘 Image")]
    public Image bigPortrait;
    [Tooltip("大立绘父节点（用于 SetActive）")]
    public GameObject bigPortraitGo;

    [Header("左侧小头像组")]
    [Tooltip("左侧组父节点（含小头像+固定名字 TMP）")]
    public GameObject leftGroup;
    [Tooltip("左侧小头像 Image")]
    public Image leftPortrait;

    [Header("右侧小头像组")]
    [Tooltip("右侧组父节点（含小头像+固定名字 TMP）")]
    public GameObject rightGroup;
    [Tooltip("右侧小头像 Image")]
    public Image rightPortrait;

    [Header("对话框")]
    [Tooltip("对话框左上角说话人名字 TMP")]
    public TMP_Text speakerNameText;
    [Tooltip("对话内容 TMP")]
    public TMP_Text dialogueText;

    [Header("TextAnimator（打字机效果）")]
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
    bool lineFullyShown;
    TMPro.TextAlignmentOptions defaultAlignment;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        dialoguePanel.SetActive(false);
        defaultAlignment = dialogueText.alignment;
    }

    /// <summary>GameManager 或其他脚本调用，开始一段对话</summary>
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
                CompleteTyping();
            else
                AdvanceDialogue();
        }
    }

    /// <summary>显示一行对话</summary>
    void ShowLine(DialogueLine line)
    {
        lineFullyShown = false;

        bool isNarration = string.IsNullOrEmpty(line.speakerName);

        if (isNarration)
        {
            // ====== 旁白模式：全部隐藏，只留居中文本 ======
            if (bigPortraitGo != null) bigPortraitGo.SetActive(false);
            if (leftGroup != null) leftGroup.SetActive(false);
            if (rightGroup != null) rightGroup.SetActive(false);
            speakerNameText.gameObject.SetActive(false);
            dialogueText.alignment = TMPro.TextAlignmentOptions.Center;
        }
        else
        {
            // ====== 角色对话模式 ======
            dialogueText.alignment = defaultAlignment;
            speakerNameText.gameObject.SetActive(true);
            speakerNameText.text = line.speakerName;

            // 站位：方波=左，其余=右
            bool isLeft = line.speakerName == "方波";

            // 大立绘
            UpdateBigPortrait(line.bigPortrait);

            // 小头像组
            UpdateSmallPortraits(line.portrait, isLeft);
        }

        // 文本
        if (currentDialogue.useTypingEffect && typewriter != null)
        {
            typewriter.ShowText(line.text);
            typewriter.onTextShowed.RemoveAllListeners();
            typewriter.onTextShowed.AddListener(() => OnTypingComplete());
        }
        else
        {
            dialogueText.text = line.text;
            OnTypingComplete();
        }

        pressHint.SetActive(false);
    }

    /// <summary>大立绘：淡出→换图→淡入</summary>
    void UpdateBigPortrait(Sprite sprite)
    {
        if (bigPortraitGo == null || bigPortrait == null) return;

        // 如果大立绘已经显示同一张图，不动
        if (bigPortraitGo.activeSelf && bigPortrait.sprite == sprite) return;

        if (!bigPortraitGo.activeSelf)
        {
            // 首次显示：直接设图 + 淡入
            bigPortraitGo.SetActive(true);
            bigPortrait.sprite = sprite;
            bigPortrait.color = new Color(1, 1, 1, 0);
            bigPortrait.DOFade(1f, portraitFadeDuration).SetUpdate(true);
        }
        else
        {
            // 切换说话人：淡出→换图→淡入
            bigPortrait.DOFade(0f, portraitFadeDuration).SetUpdate(true)
                .OnComplete(() =>
                {
                    bigPortrait.sprite = sprite;
                    bigPortrait.DOFade(1f, portraitFadeDuration).SetUpdate(true);
                });
        }
    }

    /// <summary>小头像组：说话人高亮，另一个变暗</summary>
    void UpdateSmallPortraits(Sprite sprite, bool isLeft)
    {
        GameObject activeGroup = isLeft ? leftGroup : rightGroup;
        GameObject inactiveGroup = isLeft ? rightGroup : leftGroup;
        Image activeImg = isLeft ? leftPortrait : rightPortrait;
        Image inactiveImg = isLeft ? rightPortrait : leftPortrait;

        // 说话方
        if (activeGroup != null) activeGroup.SetActive(true);
        if (activeImg != null)
        {
            activeImg.sprite = sprite;
            if (currentDialogue.useAnimation)
            {
                activeImg.color = new Color(1, 1, 1, 0);
                activeImg.DOFade(1f, portraitFadeDuration).SetUpdate(true);
            }
            else
            {
                activeImg.color = Color.white;
            }
        }

        // 非说话方：变暗
        if (inactiveGroup != null)
        {
            if (!inactiveGroup.activeSelf)
                inactiveGroup.SetActive(true);
        }
        if (inactiveImg != null && currentDialogue.useAnimation)
        {
            inactiveImg.DOFade(0.3f, portraitFadeDuration).SetUpdate(true);
        }
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
            typewriter.SkipTypewriter();
        else
        {
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
        if (typewriter != null && typewriter.isShowingText)
            typewriter.SkipTypewriter();

        dialoguePanel.SetActive(false);

        // 复位所有 UI
        if (bigPortraitGo != null) bigPortraitGo.SetActive(false);
        if (leftGroup != null) leftGroup.SetActive(false);
        if (rightGroup != null) rightGroup.SetActive(false);
        speakerNameText.gameObject.SetActive(true);
        dialogueText.alignment = defaultAlignment;

        Time.timeScale = 1f;

        var done = currentDialogue;
        currentDialogue = null;
        done?.onComplete?.Invoke();
    }

    void OnDestroy()
    {
        if (IsActive)
            Time.timeScale = 1f;
    }
}
