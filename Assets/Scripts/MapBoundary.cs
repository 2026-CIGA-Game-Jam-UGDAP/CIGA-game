using UnityEngine;

/// <summary>
/// 地图边界：PolygonCollider2D (isTrigger) 定义地图范围。
/// 玩家离开触发器区域 → 调用 GameManager.ResetLevel() 重来（有 fade 效果）。
/// </summary>
public class MapBoundary : MonoBehaviour
{
    GameManager gm;

    void Start()
    {
        gm = FindObjectOfType<GameManager>();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null && gm != null)
            gm.ResetLevel();
    }
}
