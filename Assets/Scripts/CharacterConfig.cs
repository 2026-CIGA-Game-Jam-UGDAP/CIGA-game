using UnityEngine;

/// <summary>
/// 角色配置：名字、立绘、站位。每个角色创建一个 asset。
/// </summary>
[CreateAssetMenu(menuName = "GameJam/CharacterConfig", fileName = "Character_")]
public class CharacterConfig : ScriptableObject
{
    [Tooltip("角色名（如 方波、拉兹），与 DialogueLine.speakerName 匹配")]
    public string characterName;

    [Tooltip("小头像 Sprite（对话框旁）")]
    public Sprite smallPortrait;

    [Tooltip("大立绘 Sprite（屏幕中央）")]
    public Sprite bigPortrait;

    [Tooltip("站位：true=左侧, false=右侧")]
    public bool isLeftSide;
}
