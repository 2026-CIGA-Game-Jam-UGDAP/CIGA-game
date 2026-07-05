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
        float srcRadius = 0f;
        if (moveRadius <= 0f)
        {
            // 自动读非 trigger CircleCollider2D
            foreach (var col in GetComponents<CircleCollider2D>())
            {
                if (!col.isTrigger)
                {
                    srcRadius = col.radius;
                    moveRadius = srcRadius * Mathf.Max(transform.localScale.x, transform.localScale.y);
                    break;
                }
            }
        }
        if (moveRadius <= 0f)
        {
            // 兜底：任意 CircleCollider2D（含 trigger）
            var col = GetComponent<CircleCollider2D>();
            if (col != null)
            {
                srcRadius = col.radius;
                moveRadius = srcRadius * Mathf.Max(transform.localScale.x, transform.localScale.y);
            }
        }

        // ★ 防御：moveRadius 不能为 0，否则 GetSurfacePoint/GetSurfaceNormal 会除零产生 NaN
        if (moveRadius <= 0f)
        {
            moveRadius = 0.5f;
        }

        Debug.Log($"[AnchorPoint] {name}: moveRadius={moveRadius:F2}, scale=({transform.localScale.x:F2},{transform.localScale.y:F2}), srcRadius={srcRadius:F2}", this);
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
            Debug.Log($"[AnchorPoint] {name}: Player{pc.playerIndex} 进入触发区 (当前范围人数={playersInRange.Count})");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            playersInRange.Remove(pc);
            Debug.Log($"[AnchorPoint] {name}: Player{pc.playerIndex} 离开触发区 (当前范围人数={playersInRange.Count}, 已吸附={pc.IsAnchored})");
        }
    }

    void Update()
    {
        for (int i = playersInRange.Count - 1; i >= 0; i--)
            if (playersInRange[i] == null) playersInRange.RemoveAt(i);

        // ★ 只有真正脱离（非吸附且非飞行中）才清理。飞行中的玩家暂时 isAnchored=false，不能踢
        for (int i = anchoredPlayers.Count - 1; i >= 0; i--)
            if (anchoredPlayers[i] == null || (!anchoredPlayers[i].IsAnchored && !anchoredPlayers[i].IsFlyingToAnchor))
                anchoredPlayers.RemoveAt(i);

        // ★ 收集已检查的玩家，避免 playersInRange 和 anchoredPlayers 重复处理
        var checkedPlayers = new System.Collections.Generic.HashSet<PlayerController>();

        for (int i = 0; i < playersInRange.Count; i++)
        {
            var pc = playersInRange[i];
            if (pc == null) continue;
            checkedPlayers.Add(pc);

            KeyCode key = pc.playerIndex == 0 ? PlayerController.P1_Snap : PlayerController.P2_Snap;
            if (!Input.GetKeyDown(key)) continue;

            if (pc.IsAnchored)
            {
                Debug.Log($"[AnchorPoint] {name}: Player{pc.playerIndex} 按键脱离 (当前锚点={pc.CurrentAnchor}, IsAnchored={pc.IsAnchored}, IsFlying={pc.IsFlyingToAnchor})");
                pc.DetachFromAnchor();
                anchoredPlayers.Remove(pc);
            }
            else
            {
                Debug.Log($"[AnchorPoint] {name}: Player{pc.playerIndex} 按键吸附 (IsAnchored={pc.IsAnchored}, IsFlying={pc.IsFlyingToAnchor})");
                pc.AttachToAnchor(this, snapSpeed);
                anchoredPlayers.Add(pc);
            }
        }

        // ★ 已吸附玩家即使离开触发区也能脱离
        for (int i = anchoredPlayers.Count - 1; i >= 0; i--)
        {
            var pc = anchoredPlayers[i];
            if (pc == null) continue;
            if (checkedPlayers.Contains(pc)) continue; // 上面已处理

            KeyCode key = pc.playerIndex == 0 ? PlayerController.P1_Snap : PlayerController.P2_Snap;
            if (!Input.GetKeyDown(key)) continue;

            Debug.Log($"[AnchorPoint] {name}: Player{pc.playerIndex} 按键脱离(anchoredPlayers, 已离开触发区) (IsAnchored={pc.IsAnchored})");
            pc.DetachFromAnchor();
            anchoredPlayers.RemoveAt(i);
        }

        if (indicator != null)
            indicator.color = anchoredPlayers.Count > 0 ? occupiedColor : freeColor;
    }

    // ============ Gizmos ============

    float GetGizmoRadius()
    {
        if (moveRadius > 0f) return moveRadius;
        // Editor 模式下 Awake 还没跑，直接从 collider 算
        foreach (var col in GetComponents<CircleCollider2D>())
            if (!col.isTrigger) return col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
        var fallback = GetComponent<CircleCollider2D>();
        if (fallback != null) return fallback.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
        return 0.5f;
    }

    void OnDrawGizmosSelected()
    {
        float r = GetGizmoRadius();
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
