using UnityEngine;

/// <summary>
/// 拾取后增加玩家在吸附状态（星球表面）的移动速度。
/// 挂在一个带 Collider2D + isTrigger 的 GameObject 上。
/// </summary>
public class SurfaceSpeedBoost : MonoBehaviour
{
    [Header("加速量")]
    [Tooltip("拾取后给玩家 moveSpeed 加的值")]
    public float speedBonus = 3f;

    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null) return;

        pc.moveSpeed += speedBonus;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayEnergyPickup();

        Destroy(gameObject);
    }
}
