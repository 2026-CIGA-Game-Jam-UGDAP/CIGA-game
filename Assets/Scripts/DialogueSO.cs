using UnityEngine;

/// <summary>
/// 单行对话数据
/// </summary>
[System.Serializable]
public struct DialogueLine
{
    [Tooltip("说话角色名")]
    public string speakerName;

    [TextArea(2, 5)]
    [Tooltip("对话文本")]
    public string text;

    [Tooltip("立绘 Sprite")]
    public Sprite portrait;

    [Tooltip("true=左侧站位, false=右侧站位")]
    public bool isLeftSide;
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
}
