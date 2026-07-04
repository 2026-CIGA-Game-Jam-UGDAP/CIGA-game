using UnityEngine;

/// <summary>
/// 绳索物理参数。在 Play Mode 下修改 Inspector 值即刻生效，方便调手感。
/// 创建：右键 → Create → CIGA → Rope Config
/// </summary>
[CreateAssetMenu(menuName = "CIGA/Rope Config", fileName = "RopeConfig")]
public class RopeConfig : ScriptableObject
{
    [Header("尺寸")]
    [Tooltip("绳子长度")]
    [Range(1f, 50f)]
    public float ropeLength = 10f;

    [Tooltip("绳子粗细")]
    [Range(0.01f, 0.5f)]
    public float thickness = 0.07f;

    [Header("弹性")]
    [Tooltip("拉伸刚度。越小越弹（0.1=超弹橡皮筋，1=刚性太空缆）")]
    [Range(0.01f, 1f)]
    public float stretchStiffness = 0.329f;

    [Tooltip("弯曲刚度。越小越软越容易下坠，1=太空绷直线")]
    [Range(0.01f, 1f)]
    public float bendingStiffness = 0.438f;

    [Header("断裂")]
    [Tooltip("断裂阈值。力超过此值绳子会断。设 999 则永不断")]
    [Range(1f, 999f)]
    public float tearResistance = 999f;
}
