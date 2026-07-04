using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 多边形锚点（PolygonCollider2D / BoxCollider2D）：靠近按吸附键 → 飞到表面，在边表面切向移动。
/// 再按一次 → 脱离。支持多个玩家同时吸附。
/// 吸附键定义在 PlayerController.P1_Snap / PlayerController.P2_Snap。
/// </summary>
public class PolyAnchorPoint : MonoBehaviour
{
    [Header("吸附参数")]
    [Tooltip("飞向锚点的移动速度")]
    [SerializeField] float snapSpeed = 12f;

    [Header("视觉提示（可选）")]
    [SerializeField] SpriteRenderer indicator;
    [SerializeField] Color freeColor = Color.white;
    [SerializeField] Color occupiedColor = Color.green;

    List<PlayerController> playersInRange = new List<PlayerController>();
    List<PlayerController> anchoredPlayers = new List<PlayerController>();

    // ---- 表面数据 ----
    bool polyClockwise;
    Vector2[] pathVertices;
    float[] edgeStartT;
    Vector2[] sampledPoints;
    Vector2[] sampledNormals;
    float[] accumulatedT;
    float totalPerimeter;
    float pointSpacing;

    public float SurfaceLength => totalPerimeter;
    public float MoveDirectionSign => polyClockwise ? 1f : -1f;

    void Awake()
    {
        // 1. 非 trigger PolygonCollider2D
        PolygonCollider2D surfacePoly = null;
        foreach (var p in GetComponents<PolygonCollider2D>())
        {
            if (!p.isTrigger && p.pathCount > 0) { surfacePoly = p; break; }
        }
        if (surfacePoly != null)
        {
            SamplePolygon(surfacePoly);
            Debug.Log($"[PolyAnchorPoint] {name}: Polygon {surfacePoly.GetPath(0).Length}pts, perimeter={totalPerimeter:F2}, scale=({transform.localScale.x:F2},{transform.localScale.y:F2})");
            return;
        }

        // 2. 非 trigger BoxCollider2D
        BoxCollider2D surfaceBox = null;
        foreach (var b in GetComponents<BoxCollider2D>())
        {
            if (!b.isTrigger) { surfaceBox = b; break; }
        }
        if (surfaceBox != null)
        {
            SampleBoxCollider(surfaceBox);
            Debug.Log($"[PolyAnchorPoint] {name}: Box, perimeter={totalPerimeter:F2}, scale=({transform.localScale.x:F2},{transform.localScale.y:F2})");
            return;
        }

        // 3. 兜底：任意 PolygonCollider2D（含 trigger）
        var fallback = GetComponent<PolygonCollider2D>();
        if (fallback != null && fallback.pathCount > 0)
        {
            SamplePolygon(fallback);
            Debug.Log($"[PolyAnchorPoint] {name}: Fallback Polygon (trigger), perimeter={totalPerimeter:F2}");
            return;
        }

        // 4. 兜底：任意 BoxCollider2D（含 trigger）
        var fallbackBox = GetComponent<BoxCollider2D>();
        if (fallbackBox != null)
        {
            SampleBoxCollider(fallbackBox);
            Debug.Log($"[PolyAnchorPoint] {name}: Fallback Box (trigger), perimeter={totalPerimeter:F2}");
            return;
        }

        Debug.LogError($"[PolyAnchorPoint] {name}: 未找到 PolygonCollider2D 或 BoxCollider2D！请在 GameObject 上添加一个。");
        // ★ 防御：设置兜底值避免除零 NaN
        totalPerimeter = 1f;
        pointSpacing = 0.1f;
        pathVertices = new Vector2[] { Vector2.zero, Vector2.right };
        edgeStartT = new float[] { 0f, 1f };
        sampledPoints = new Vector2[] { Vector2.zero, Vector2.right };
        sampledNormals = new Vector2[] { Vector2.up, Vector2.up };
        accumulatedT = new float[] { 0f, 1f };
    }

    void SamplePolygon(PolygonCollider2D poly)
    {
        Vector2[] raw = poly.GetPath(0);
        Vector3 scl = transform.localScale;
        Vector2[] path = new Vector2[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            path[i] = Vector2.Scale(raw[i], scl);
        pathVertices = path;

        float signedArea = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[(i + 1) % path.Length];
            signedArea += a.x * b.y - b.x * a.y;
        }
        polyClockwise = signedArea < 0f;

        totalPerimeter = 0f;
        edgeStartT = new float[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            edgeStartT[i] = totalPerimeter;
            totalPerimeter += Vector2.Distance(path[i], path[(i + 1) % path.Length]);
        }

        int pointCount = Mathf.Max(32, Mathf.CeilToInt(totalPerimeter / 0.1f));
        pointSpacing = totalPerimeter / pointCount;
        sampledPoints = new Vector2[pointCount];
        sampledNormals = new Vector2[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float t = i * pointSpacing;
            GetPointOnPath(t, out Vector2 pt, out Vector2 n);
            sampledPoints[i] = pt;
            sampledNormals[i] = n;
        }

        accumulatedT = new float[pointCount];
        for (int i = 0; i < pointCount; i++)
            accumulatedT[i] = i * pointSpacing;
    }

    void SampleBoxCollider(BoxCollider2D box)
    {
        Vector3 scl = transform.localScale;
        Vector2 half = Vector2.Scale(box.size * 0.5f, scl);
        Vector2 off = Vector2.Scale(box.offset, scl);

        Vector2[] path = new Vector2[4];
        path[0] = new Vector2( half.x,  half.y) + off;
        path[1] = new Vector2( half.x, -half.y) + off;
        path[2] = new Vector2(-half.x, -half.y) + off;
        path[3] = new Vector2(-half.x,  half.y) + off;
        pathVertices = path;
        polyClockwise = true;

        totalPerimeter = 0f;
        edgeStartT = new float[4];
        for (int i = 0; i < 4; i++)
        {
            edgeStartT[i] = totalPerimeter;
            totalPerimeter += Vector2.Distance(path[i], path[(i + 1) % 4]);
        }

        int pointCount = Mathf.Max(32, Mathf.CeilToInt(totalPerimeter / 0.1f));
        pointSpacing = totalPerimeter / pointCount;
        sampledPoints = new Vector2[pointCount];
        sampledNormals = new Vector2[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            float t = i * pointSpacing;
            GetPointOnPath(t, out Vector2 pt, out Vector2 n);
            sampledPoints[i] = pt;
            sampledNormals[i] = n;
        }

        accumulatedT = new float[pointCount];
        for (int i = 0; i < pointCount; i++)
            accumulatedT[i] = i * pointSpacing;
    }

    void GetPointOnPath(float t, out Vector2 point, out Vector2 normal)
    {
        float accumulated = 0f;
        for (int i = 0; i < pathVertices.Length; i++)
        {
            Vector2 start = pathVertices[i];
            Vector2 end = pathVertices[(i + 1) % pathVertices.Length];
            float edgeLen = Vector2.Distance(start, end);
            if (edgeLen < 0.0001f) continue;

            if (accumulated + edgeLen >= t)
            {
                float edgeT = Mathf.Clamp01((t - accumulated) / edgeLen);
                point = Vector2.Lerp(start, end, edgeT);
                Vector2 edgeDir = (end - start).normalized;
                Vector2 leftNormal = new Vector2(-edgeDir.y, edgeDir.x);
                normal = polyClockwise ? leftNormal : -leftNormal;
                return;
            }
            accumulated += edgeLen;
        }

        point = pathVertices[pathVertices.Length - 1];
        normal = Vector2.up;
    }

    // ============ 表面接口 ============

    int FindSampleIndex(float t)
    {
        int lo = 0, hi = accumulatedT.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (accumulatedT[mid] <= t) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    public Vector2 GetSurfacePoint(float t)
    {
        t = WrapT(t);
        int idx = FindSampleIndex(t);
        int next;
        float baseT = accumulatedT[idx];
        float segLen;
        if (idx == accumulatedT.Length - 1)
        {
            next = 0;
            segLen = totalPerimeter - baseT;
        }
        else
        {
            next = idx + 1;
            segLen = accumulatedT[next] - baseT;
        }
        float frac = segLen > 0.0001f ? Mathf.Clamp01((t - baseT) / segLen) : 0f;
        Vector2 local = Vector2.Lerp(sampledPoints[idx], sampledPoints[next], frac);
        return (Vector2)transform.position + local;
    }

    public Vector2 GetSurfaceNormal(float t)
    {
        t = WrapT(t);

        int edgeIdx = -1;
        for (int i = 0; i < pathVertices.Length; i++)
        {
            float edgeEnd = (i == pathVertices.Length - 1) ? totalPerimeter : edgeStartT[i + 1];
            if (t >= edgeStartT[i] && t < edgeEnd) { edgeIdx = i; break; }
        }
        if (edgeIdx < 0) edgeIdx = pathVertices.Length - 1;

        Vector2 start = pathVertices[edgeIdx];
        Vector2 end = pathVertices[(edgeIdx + 1) % pathVertices.Length];
        Vector2 edgeDir = (end - start).normalized;
        Vector2 leftNormal = new Vector2(-edgeDir.y, edgeDir.x);
        return polyClockwise ? leftNormal : -leftNormal;
    }

    public float FindClosestSurfaceT(Vector3 worldPos)
    {
        Vector2 local = worldPos - transform.position;

        int bestEdge = 0;
        float bestDist = float.MaxValue;
        float bestEdgeT = 0f;

        for (int i = 0; i < pathVertices.Length; i++)
        {
            Vector2 a = pathVertices[i];
            Vector2 b = pathVertices[(i + 1) % pathVertices.Length];
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 0.0001f) continue;

            float edgeT = Mathf.Clamp01(Vector2.Dot(local - a, ab) / abLenSq);
            Vector2 closest = a + edgeT * ab;
            float dSq = (local - closest).sqrMagnitude;

            if (dSq < bestDist)
            {
                bestDist = dSq;
                bestEdge = i;
                bestEdgeT = edgeT;
            }
        }

        Vector2 edgeA = pathVertices[bestEdge];
        Vector2 edgeB = pathVertices[(bestEdge + 1) % pathVertices.Length];
        float edgeLen = Vector2.Distance(edgeA, edgeB);

        float epsilon = Mathf.Min(edgeLen * 0.001f, 0.001f);
        if (bestEdgeT < epsilon) bestEdgeT = epsilon;
        else if (bestEdgeT > 1f - epsilon) bestEdgeT = 1f - epsilon;

        return edgeStartT[bestEdge] + bestEdgeT * edgeLen;
    }

    public Vector2 GetClosestSurfacePoint(Vector3 worldPos)
    {
        float t = FindClosestSurfaceT(worldPos);
        return GetSurfacePoint(t);
    }

    float WrapT(float t)
    {
        if (totalPerimeter <= 0f) return 0f;
        t = t % totalPerimeter;
        if (t < 0f) t += totalPerimeter;
        return t;
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
            Debug.Log($"[PolyAnchorPoint] {pc.name} 进入吸附范围");
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
        if (pathVertices == null || pathVertices.Length == 0) return;

        // 边 + 法线
        for (int i = 0; i < pathVertices.Length; i++)
        {
            Vector2 a = (Vector2)transform.position + pathVertices[i];
            Vector2 b = (Vector2)transform.position + pathVertices[(i + 1) % pathVertices.Length];

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(a, b);

            Vector2 mid = (a + b) * 0.5f;
            Vector2 edgeDir = (b - a).normalized;
            Vector2 leftNormal = new Vector2(-edgeDir.y, edgeDir.x);
            Vector2 outward = polyClockwise ? leftNormal : -leftNormal;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(mid, mid + outward * 0.5f);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(a, 0.05f);
        }

        // 采样点（每隔 4 个）
        if (sampledPoints != null && sampledNormals != null)
        {
            Gizmos.color = Color.magenta;
            for (int i = 0; i < sampledPoints.Length; i += 4)
            {
                Vector2 wp = (Vector2)transform.position + sampledPoints[i];
                Gizmos.DrawSphere(wp, 0.03f);
                Gizmos.DrawLine(wp, wp + sampledNormals[i] * 0.3f);
            }
        }
    }
}
