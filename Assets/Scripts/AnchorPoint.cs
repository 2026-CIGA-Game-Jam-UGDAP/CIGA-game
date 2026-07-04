using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 圆形锚点：靠近按吸附键 → 飞到表面，在圆形表面切向移动。
/// 再按一次 → 脱离。支持多个玩家同时吸附。
/// 吸附键定义在 PlayerController.P1_Snap / PlayerController.P2_Snap。
/// </summary>
public class AnchorPoint : MonoBehaviour
{
    [Header("吸附参数")]
    [Tooltip("飞向锚点的移动速度")]
    [SerializeField] float snapSpeed = 12f;
    [Tooltip("吸附后玩家可移动半径。0=自动读取 CircleCollider2D")]
    [SerializeField] float moveRadius = 0f;

    [Header("视觉提示（可选）")]
    [SerializeField] SpriteRenderer indicator;
    [SerializeField] Color freeColor = Color.white;
    [SerializeField] Color occupiedColor = Color.green;

    List<PlayerController> playersInRange = new List<PlayerController>();
    List<PlayerController> anchoredPlayers = new List<PlayerController>();

    public float SurfaceLength => 2f * Mathf.PI * moveRadius;
    /// <summary>t 增加方向 = 逆时针，所以左右移动符号为 -1</summary>
    public float MoveDirectionSign => -1f;

    void Awake()
    {
        if (moveRadius <= 0f)
        {
            // 自动读非 trigger CircleCollider2D
            foreach (var col in GetComponents<CircleCollider2D>())
            {
                if (!col.isTrigger)
                {
                    moveRadius = col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
                    break;
                }
            }
        }
        if (moveRadius <= 0f)
        {
            // 兜底：任意 CircleCollider2D（含 trigger）
            var col = GetComponent<CircleCollider2D>();
            if (col != null) moveRadius = col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
        }

        // ★ 防御：moveRadius 不能为 0，否则 GetSurfacePoint/GetSurfaceNormal 会除零产生 NaN
        if (moveRadius <= 0f)
        {
            moveRadius = 0.5f;
            Debug.LogWarning($"[AnchorPoint] {name}: 未找到有效半径，使用兜底值 0.5。请检查是否缺少 CircleCollider2D 或 scale 为 0。");
        }

        Debug.Log($"[AnchorPoint] {name}: moveRadius={moveRadius:F2}, scale=({transform.localScale.x:F2},{transform.localScale.y:F2})");
    }

    // ============ 圆形表面接口 ============

    public Vector2 GetSurfacePoint(float t)
    {
        if (moveRadius <= 0f) return transform.position;
        float angle = t / moveRadius;
        return (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * moveRadius;
    }

    public Vector2 GetSurfaceNormal(float t)
    {
        if (moveRadius <= 0f) return Vector2.up;
        float angle = t / moveRadius;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    public float FindClosestSurfaceT(Vector3 worldPos)
    {
        Vector2 local = worldPos - transform.position;
        float angle = Mathf.Atan2(local.y, local.x);
        return angle * moveRadius;
    }

    public Vector2 GetClosestSurfacePoint(Vector3 worldPos)
    {
        float t = FindClosestSurfaceT(worldPos);
        return GetSurfacePoint(t);
    }

    // ============ 吸附/脱离逻辑 ============

    void Start()
    {
        if (indicator != null) indicator.color = freeColor;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc != null && !playersInRange.Contains(pc))
        {
            playersInRange.Add(pc);
            Debug.Log($"[AnchorPoint] {pc.name} 进入吸附范围");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc != null) playersInRange.Remove(pc);
    }

    void Update()
    {
        for (int i = playersInRange.Count - 1; i >= 0; i--)
            if (playersInRange[i] == null) playersInRange.RemoveAt(i);

        for (int i = anchoredPlayers.Count - 1; i >= 0; i--)
            if (anchoredPlayers[i] == null || !anchoredPlayers[i].IsAnchored)
                anchoredPlayers.RemoveAt(i);

        for (int i = 0; i < playersInRange.Count; i++)
        {
            var pc = playersInRange[i];
            if (pc == null) continue;

            KeyCode key = pc.playerIndex == 0 ? PlayerController.P1_Snap : PlayerController.P2_Snap;
            if (!Input.GetKeyDown(key)) continue;

            if (pc.IsAnchored)
            {
                pc.DetachFromAnchor();
                anchoredPlayers.Remove(pc);
            }
            else
            {
                pc.AttachToAnchor(this, snapSpeed);
                anchoredPlayers.Add(pc);
            }

            if (indicator != null)
                indicator.color = anchoredPlayers.Count > 0 ? occupiedColor : freeColor;
        }
    }

    // ============ Gizmos ============

    void OnDrawGizmosSelected()
    {
        float r = moveRadius > 0 ? moveRadius : 0.5f;
        Vector3 center = transform.position;

        Gizmos.color = Color.cyan;
        int segs = 32;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * 2f * Mathf.PI / segs;
            float a1 = (i + 1) * 2f * Mathf.PI / segs;
            Gizmos.DrawLine(
                center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * r,
                center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * r);
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            Vector2 outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 pos = (Vector2)center + outward * r;
            Gizmos.DrawLine(pos, pos + outward * 0.5f);
        }
    }
}
