using UnityEngine;

/// <summary>
/// 太空零件拾取物。两种类型：
///   Goal   — 通关必需零件，收集齐了过关
///   Energy — 喷气背包能量，靠近自动拾取
/// 挂场景中的 GameObject 上，需要 Collider2D + isTrigger。
/// </summary>
public class Pickup : MonoBehaviour
{
    public enum Type { Goal, Energy }

    [Header("类型")]
    [Tooltip("Goal=通关零件（收集齐过关），Energy=能量补给（靠近自动拾取）")]
    public Type pickupType = Type.Energy;

    [Header("能量恢复量（仅 Energy 类型有效）")]
    [Tooltip("拾取后恢复的能量值")]
    public float energyAmount = 3f;

    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        if (pickupType == Type.Energy)
        {
            pc.AddEnergy(energyAmount);
        }
        else if (pickupType == Type.Goal)
        {
            var gm = FindObjectOfType<GameManager>();
            if (gm != null) gm.OnGoalPickupCollected();
        }

        Destroy(gameObject);
    }
}
