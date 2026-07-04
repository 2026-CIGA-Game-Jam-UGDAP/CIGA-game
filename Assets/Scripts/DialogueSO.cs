using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 单行对话数据。portrait/bigPortrait 由 CreateAllDialogues 工具根据 CharacterConfig 自动填充。
/// </summary>
[System.Serializable]
public struct DialogueLine
{
    [Tooltip("说话角色名，留空=旁白/教程（隐藏立绘+名字，文本居中）")]
    public string speakerName;

    [TextArea(2, 5)]
    [Tooltip("对话文本")]
    public string text;

    [Tooltip("小头像 Sprite（自动填充自 CharacterConfig）")]
    public Sprite portrait;

    [Tooltip("大立绘 Sprite（屏幕中央，自动填充自 CharacterConfig）")]
    public Sprite bigPortrait;
}

/// <summary>
/// 一段对话的 ScriptableObject。
/// 在 Project 窗口右键 → Create → GameJam → Dialogue 创建。
/// </summary>
[CreateAssetMenu(menuName = "GameJam/Dialogue", fileName = "Dialogue_")]
public class DialogueSO : ScriptableObject
{
    [Header("对话内容")]
    [Tooltip("对话行列表，按顺序播放")]
    public DialogueLine[] lines;

    [Header("迭代开关")]
    [Tooltip("启用打字机逐字效果（需 TextAnimator TypewriterByCharacter 组件）")]
    public bool useTypingEffect;

    [Tooltip("启用立绘进出场动画（DOTween 淡入淡出）")]
    public bool useAnimation;

    [Header("标记")]
    [Tooltip("是否需要 CG 演出（仅标记，暂不实现逻辑）")]
    public bool needsCG;

    [Header("事件")]
    [Tooltip("对话结束后触发。可拖 GameManager 的方法来推进游戏状态")]
    public UnityEvent onComplete;
}
