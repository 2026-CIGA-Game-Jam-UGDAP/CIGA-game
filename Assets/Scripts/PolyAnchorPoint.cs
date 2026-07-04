using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 多边形锚点（PolygonCollider2D / BoxCollider2D）：靠近按吸附键 → 飞到表面，在边表面切向移动。
/// 再按一次 → 脱离。支持多个玩家同时吸附。
/// 法线方向用几何中心判定（本地空间），不依赖 winding order 或 Collider 查询。
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
    Vector2 polyCenter;               // 多边形几何中心（本地空间），用于判定法线朝向
    Vector2[] pathVertices;
    Vector2[] edgeOutwardNormals;  // ★ 预计算每条边的朝外法线（本地空间）
    float[] edgeStartT;
    Vector2[] sampledPoints;
    Vector2[] sampledNormals;
    float[] accumulatedT;
    float totalPerimeter;
    float pointSpacing;
    float moveDirectionSign;       // ★ 预计算的移动方向符号

    public float SurfaceLength => totalPerimeter;
    public float MoveDirectionSign => moveDirectionSign;

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
            return;
        }

        // 3. 兜底：任意 PolygonCollider2D（含 trigger）
        var fallbackPoly = GetComponent<PolygonCollider2D>();
        if (fallbackPoly != null && fallbackPoly.pathCount > 0)
        {
            SamplePolygon(fallbackPoly);
            return;
        }

        // 4. 兜底：任意 BoxCollider2D（含 trigger）
        var fallbackBox = GetComponent<BoxCollider2D>();
        if (fallbackBox != null)
        {
            SampleBoxCollider(fallbackBox);
            return;
        }

        totalPerimeter = 1f;
        pointSpacing = 0.1f;
        polyCenter = Vector2.zero;
        pathVertices = new Vector2[] { Vector2.zero, Vector2.right };
        edgeOutwardNormals = new Vector2[] { Vector2.up, Vector2.up };
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

        // ★ 计算周长和边起始 t
        totalPerimeter = 0f;
        edgeStartT = new float[path.Length];
        float[] edgeLengths = new float[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            edgeStartT[i] = totalPerimeter;
            edgeLengths[i] = Vector2.Distance(path[i], path[(i + 1) % path.Length]);
            totalPerimeter += edgeLengths[i];
        }

        // ★ 先算几何中心（本地空间），ComputeOutwardNormal 依赖它
        polyCenter = Vector2.zero;
        for (int i = 0; i < path.Length; i++)
            polyCenter += path[i];
        polyCenter /= path.Length;

        // ★ 用几何中心判定每条边的朝外法线
        edgeOutwardNormals = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 start = path[i];
            Vector2 end = path[(i + 1) % path.Length];
            edgeOutwardNormals[i] = ComputeOutwardNormal(start, end);
        }

        // ★ 从第一条有效边推导 MoveDirectionSign
        moveDirectionSign = ComputeMoveDirectionSign();

        // ★ 采样表面点（用预计算的法线）
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
        path[0] = new Vector2( half.x,  half.y) + off;  // 右上
        path[1] = new Vector2( half.x, -half.y) + off;  // 右下
        path[2] = new Vector2(-half.x, -half.y) + off;  // 左下
        path[3] = new Vector2(-half.x,  half.y) + off;  // 左上
        pathVertices = path;

        totalPerimeter = 0f;
        edgeStartT = new float[4];
        float[] edgeLengths = new float[4];
        for (int i = 0; i < 4; i++)
        {
            edgeStartT[i] = totalPerimeter;
            edgeLengths[i] = Vector2.Distance(path[i], path[(i + 1) % 4]);
            totalPerimeter += edgeLengths[i];
        }

        // ★ 先算几何中心
        polyCenter = Vector2.zero;
        for (int i = 0; i < 4; i++)
            polyCenter += path[i];
        polyCenter /= 4f;

        // ★ 用几何中心判定每条边的朝外法线
        edgeOutwardNormals = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Vector2 start = path[i];
            Vector2 end = path[(i + 1) % 4];
            edgeOutwardNormals[i] = ComputeOutwardNormal(start, end);
        }

        moveDirectionSign = ComputeMoveDirectionSign();

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

    /// <summary>
    /// 判定朝外法线：沿边方向的两个垂直方向中，指向远离多边形几何中心的方向即为朝外。
    /// 纯本地空间计算，不依赖 Collider 查询，不受 transform scale 影响。
    /// 适用于凸多边形和大多数简单凹多边形。
    /// </summary>
    Vector2 ComputeOutwardNormal(Vector2 start, Vector2 end)
    {
        Vector2 edgeDir = (end - start).normalized;
        Vector2 perpA = new Vector2(-edgeDir.y, edgeDir.x);   // 左侧垂直
        Vector2 perpB = new Vector2(edgeDir.y, -edgeDir.x);   // 右侧垂直

        Vector2 localMid = (start + end) * 0.5f;
        Vector2 centerToMid = localMid - polyCenter;

        // dotA > 0: perpA 与 centerToMid 同向 → 远离中心 → 朝外
        // dotA < 0: perpA 与 centerToMid 反向 → 指向中心 → 朝内 → perpB 朝外
        float dotA = Vector2.Dot(perpA, centerToMid);
        if (dotA >= 0f) return perpA;  // perpA 朝外
        return perpB;                  // perpB 朝外
    }

    /// <summary>
    /// 推导移动方向符号：玩家按右键时，surfaceT 是增加还是减少？
    /// 检查第一条有效边：edgeDir 与 (outwardNormal 的右垂直) 是否同向。
    /// </summary>
    float ComputeMoveDirectionSign()
    {
        for (int i = 0; i < pathVertices.Length; i++)
        {
            Vector2 start = pathVertices[i];
            Vector2 end = pathVertices[(i + 1) % pathVertices.Length];
            float len = Vector2.Distance(start, end);
            if (len < 0.0001f) continue;

            Vector2 edgeDir = (end - start).normalized;
            Vector2 n = edgeOutwardNormals[i];
            // 玩家面朝朝外法线 n，玩家的"右"方向 = n 顺时针旋转 90° = (n.y, -n.x)
            Vector2 rightPerp = new Vector2(n.y, -n.x);
            float dot = Vector2.Dot(edgeDir, rightPerp);
            if (Mathf.Abs(dot) < 0.001f) continue;

            // edgeDir 与 rightPerp 同向 → t 增大 = 向右 → sign = +1
            // edgeDir 与 rightPerp 反向 → t 增大 = 向左 → sign = -1
            return Mathf.Sign(dot);
        }
        return -1f; // 兜底
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
                normal = edgeOutwardNormals[i]; // ★ 使用预计算的法线
                return;
            }
            accumulated += edgeLen;
        }

        point = pathVertices[pathVertices.Length - 1];
        normal = edgeOutwardNormals.Length > 0 ? edgeOutwardNormals[edgeOutwardNormals.Length - 1] : Vector2.up;
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

    /// <summary>
    /// ★ 改用与 GetSurfacePoint 相同的采样查找，保证位置和法线完全一致。
    /// 不再重新从边几何推算，避免两套数据结构边界不一致。
    /// </summary>
    public Vector2 GetSurfaceNormal(float t)
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
        Vector2 n = Vector2.Lerp(sampledNormals[idx], sampledNormals[next], frac).normalized;
        return n;
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
            // ★ 使用预计算的法线
            Vector2 outward = edgeOutwardNormals != null && i < edgeOutwardNormals.Length
                ? edgeOutwardNormals[i]
                : Vector2.up;
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
