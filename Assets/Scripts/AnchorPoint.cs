using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 锚点：靠近按吸附键 → 飞到表面，在表面切向移动。
/// 再按一次 → 脱离。支持多个玩家同时吸附。
/// 支持圆形（CircleCollider2D）和多边形（PolygonCollider2D）。
/// P1=F, P2=RightShift。
/// </summary>
public class AnchorPoint : MonoBehaviour
{
    [Header("吸附参数")]
    [Tooltip("飞向锚点的移动速度")]
    [SerializeField] float snapSpeed = 12f;
    [Tooltip("吸附后玩家可移动半径。0=自动读取 Collider")]
    public float moveRadius = 0f;

    [Header("按键")]
    [Tooltip("P1 (WASD 玩家) 吸附/脱离按键")]
    [SerializeField] KeyCode p1SnapKey = KeyCode.F;
    [Tooltip("P2 (方向键玩家) 吸附/脱离按键")]
    [SerializeField] KeyCode p2SnapKey = KeyCode.RightShift;

    [Header("视觉提示（可选）")]
    [Tooltip("锚点指示器 SpriteRenderer，显示锚点状态颜色")]
    [SerializeField] SpriteRenderer indicator;
    [Tooltip("锚点空闲时的颜色")]
    [SerializeField] Color freeColor = Color.white;
    [Tooltip("锚点被占用时的颜色")]
    [SerializeField] Color occupiedColor = Color.green;

    List<PlayerController> playersInRange = new List<PlayerController>();
    List<PlayerController> anchoredPlayers = new List<PlayerController>();

    // ---- 表面数据（圆形和多边形统一） ----
    bool isPolygon;
    Vector2[] sampledPoints;    // 采样点（本地坐标）
    Vector2[] sampledNormals;   // 每点的向外法线
    float totalPerimeter;       // 总周长
    float pointSpacing;         // 采样点间距
    Vector2 polyCentroid;       // 多边形重心（判断法线方向用）

    public bool IsPolygon => isPolygon;
    public float SurfaceLength => isPolygon ? totalPerimeter : (2f * Mathf.PI * moveRadius);

    void Awake()
    {
        // —— 尝试读非 trigger PolygonCollider2D（和圆一样，优先非 trigger） ——
        var polys = GetComponents<PolygonCollider2D>();
        PolygonCollider2D surfacePoly = null;
        foreach (var p in polys)
        {
            if (!p.isTrigger && p.pathCount > 0) { surfacePoly = p; break; }
        }
        if (surfacePoly == null && polys.Length > 0 && polys[0].pathCount > 0)
            surfacePoly = polys[0]; // 回退：全是 trigger → 读第一个
        if (surfacePoly != null)
        {
            isPolygon = true;
            SamplePolygon(surfacePoly);
        }

        // —— 回退圆：moveRadius 优先手动值，否则自动读非 trigger CircleCollider2D ——
        if (!isPolygon && moveRadius <= 0f)
        {
            var cols = GetComponents<CircleCollider2D>();
            foreach (var col in cols)
            {
                if (!col.isTrigger)
                {
                    moveRadius = col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
                    break;
                }
            }
            if (moveRadius <= 0f)
            {
                var col = GetComponent<CircleCollider2D>();
                if (col != null) moveRadius = col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
            }
        }
    }

    /// <summary>沿 PolygonCollider2D 轮廓均匀采样</summary>
    void SamplePolygon(PolygonCollider2D poly)
    {
        Vector2[] path = poly.GetPath(0);

        // 计算重心
        polyCentroid = Vector2.zero;
        foreach (var v in path) polyCentroid += v;
        polyCentroid /= path.Length;

        // 计算总周长
        totalPerimeter = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 next = path[(i + 1) % path.Length];
            totalPerimeter += Vector2.Distance(path[i], next);
        }

        // 采样密度：每 0.1 单位一个点，最少 32 个
        int pointCount = Mathf.Max(32, Mathf.CeilToInt(totalPerimeter / 0.1f));
        pointSpacing = totalPerimeter / pointCount;
        sampledPoints = new Vector2[pointCount];
        sampledNormals = new Vector2[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float t = i * pointSpacing;
            GetPointOnPath(path, t, out Vector2 pt, out Vector2 n);
            sampledPoints[i] = pt;
            sampledNormals[i] = n;
        }
    }

    /// <summary>在 path 上距离 t 处取点 + 向外法线</summary>
    void GetPointOnPath(Vector2[] path, float t, out Vector2 point, out Vector2 normal)
    {
        float accumulated = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 start = path[i];
            Vector2 end = path[(i + 1) % path.Length];
            float edgeLen = Vector2.Distance(start, end);
            if (edgeLen < 0.0001f) continue;

            if (accumulated + edgeLen >= t)
            {
                float edgeT = Mathf.Clamp01((t - accumulated) / edgeLen);
                point = Vector2.Lerp(start, end, edgeT);

                // 向外法线：垂直于边，指离重心
                Vector2 edgeDir = (end - start).normalized;
                Vector2 leftNormal = new Vector2(-edgeDir.y, edgeDir.x);
                Vector2 edgeMid = (start + end) * 0.5f;
                normal = Vector2.Dot(leftNormal, edgeMid - polyCentroid) > 0f
                    ? leftNormal : -leftNormal;
                return;
            }
            accumulated += edgeLen;
        }

        // fallback
        point = path[path.Length - 1];
        normal = Vector2.up;
    }

    // ============ 统一表面接口 ============

    /// <summary>根据表面距离 t 获取世界坐标位置</summary>
    public Vector2 GetSurfacePoint(float t)
    {
        if (isPolygon)
        {
            t = WrapT(t);
            int idx = Mathf.FloorToInt(t / pointSpacing);
            int next = (idx + 1) % sampledPoints.Length;
            float frac = (t - idx * pointSpacing) / pointSpacing;
            Vector2 local = Vector2.Lerp(sampledPoints[idx], sampledPoints[next], frac);
            return (Vector2)transform.position + local;
        }
        else
        {
            float angle = t / moveRadius;
            Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return (Vector2)transform.position + outward * moveRadius;
        }
    }

    /// <summary>根据表面距离 t 获取向外法线（头朝向）</summary>
    public Vector2 GetSurfaceNormal(float t)
    {
        if (isPolygon)
        {
            t = WrapT(t);
            int idx = Mathf.FloorToInt(t / pointSpacing);
            int next = (idx + 1) % sampledNormals.Length;
            float frac = (t - idx * pointSpacing) / pointSpacing;
            return Vector2.Lerp(sampledNormals[idx], sampledNormals[next], frac).normalized;
        }
        else
        {
            float angle = t / moveRadius;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    /// <summary>给定世界坐标，找到表面上最近点的 t 值</summary>
    public float FindClosestSurfaceT(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;

        if (isPolygon)
        {
            int closest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < sampledPoints.Length; i++)
            {
                float d = ((Vector2)local - sampledPoints[i]).sqrMagnitude;
                if (d < best) { best = d; closest = i; }
            }
            return closest * pointSpacing;
        }
        else
        {
            float angle = Mathf.Atan2(local.y, local.x);
            return angle * moveRadius;
        }
    }

    /// <summary>给定世界坐标，返回表面上最近点的世界坐标</summary>
    public Vector2 GetClosestSurfacePoint(Vector3 worldPos)
    {
        return GetSurfacePoint(FindClosestSurfaceT(worldPos));
    }

    float WrapT(float t)
    {
        t = t % totalPerimeter;
        if (t < 0f) t += totalPerimeter;
        return t;
    }

    // ============ 吸附/脱离逻辑 ============

    void Start()
    {
        if (indicator != null)
            indicator.color = freeColor;
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
        if (pc != null)
            playersInRange.Remove(pc);
    }

    void Update()
    {
        // 清理 null
        for (int i = playersInRange.Count - 1; i >= 0; i--)
            if (playersInRange[i] == null) playersInRange.RemoveAt(i);

        for (int i = anchoredPlayers.Count - 1; i >= 0; i--)
            if (anchoredPlayers[i] == null || !anchoredPlayers[i].IsAnchored)
                anchoredPlayers.RemoveAt(i);

        for (int i = 0; i < playersInRange.Count; i++)
        {
            var pc = playersInRange[i];
            if (pc == null) continue;

            KeyCode key = pc.playerIndex == 0 ? p1SnapKey : p2SnapKey;
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
}
