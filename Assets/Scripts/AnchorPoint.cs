using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 锚点：靠近按吸附键 → 飞到表面，在表面切向移动。
/// 再按一次 → 脱离。支持多个玩家同时吸附。
/// 支持圆形（CircleCollider2D）和多边形（PolygonCollider2D）。
/// 吸附键定义在 PlayerController.P1_Snap / PlayerController.P2_Snap。
/// </summary>
public class AnchorPoint : MonoBehaviour
{
    [Header("吸附参数")]
    [Tooltip("飞向锚点的移动速度")]
    [SerializeField] float snapSpeed = 12f;
    [Tooltip("吸附后玩家可移动半径。0=自动读取 Collider")]
    public float moveRadius = 0f;
    [Tooltip("表面向外偏移量（避免玩家碰撞体与实体 collider 重叠）")]
    [SerializeField] float outwardOffset = 0.35f;

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
    bool polyClockwise;         // 多边形 winding order（决定法线方向）
    Vector2[] pathVertices;     // 多边形原始顶点（本地坐标），用于边线段投影
    float[] edgeStartT;         // 每条边起点对应的周长距离
    Vector2[] sampledPoints;    // 采样点（本地坐标）
    Vector2[] sampledNormals;   // 每点的向外法线
    float[] accumulatedT;       // 每个采样点对应的精确周长距离（避免 FloorToInt(t/pointSpacing) 浮点误差）
    float totalPerimeter;       // 总周长
    float pointSpacing;         // 采样点间距

    public bool IsPolygon => isPolygon;
    public float SurfaceLength => isPolygon ? totalPerimeter : (2f * Mathf.PI * moveRadius);

    void Awake()
    {
        // —— 尝试读非 trigger PolygonCollider2D ——
        var polys = GetComponents<PolygonCollider2D>();
        PolygonCollider2D surfacePoly = null;
        foreach (var p in polys)
        {
            if (!p.isTrigger && p.pathCount > 0) { surfacePoly = p; break; }
        }
        if (surfacePoly == null && polys.Length > 0 && polys[0].pathCount > 0)
            surfacePoly = polys[0];
        if (surfacePoly != null)
        {
            isPolygon = true;
            SamplePolygon(surfacePoly);
        }

        // —— BoxCollider2D：四角转成 polygon path ——
        if (!isPolygon)
        {
            var boxes = GetComponents<BoxCollider2D>();
            BoxCollider2D surfaceBox = null;
            foreach (var b in boxes)
            {
                if (!b.isTrigger) { surfaceBox = b; break; }
            }
            if (surfaceBox == null && boxes.Length > 0)
                surfaceBox = boxes[0];
            if (surfaceBox != null)
            {
                isPolygon = true;
                SampleBoxCollider(surfaceBox);
            }
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

        Debug.Log($"[AnchorPoint] {name}: isPolygon={isPolygon}, moveRadius={moveRadius:F2}, perimeter={totalPerimeter:F2}, scale=({transform.localScale.x:F2},{transform.localScale.y:F2})");
    }

    /// <summary>沿 PolygonCollider2D 轮廓均匀采样</summary>
    void SamplePolygon(PolygonCollider2D poly)
    {
        Vector2[] raw = poly.GetPath(0);
        Vector3 scl = transform.localScale;
        Vector2[] path = new Vector2[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            path[i] = Vector2.Scale(raw[i], scl);
        pathVertices = path;

        // 用 signed area 判断 winding order（clockwise = 内部在边的右侧）
        float signedArea = 0f;
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 a = path[i];
            Vector2 b = path[(i + 1) % path.Length];
            signedArea += a.x * b.y - b.x * a.y;
        }
        polyClockwise = signedArea < 0f;

        // 计算总周长 + 每条边起始 t
        totalPerimeter = 0f;
        edgeStartT = new float[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            edgeStartT[i] = totalPerimeter;
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

        // 累距数组：消除 FloorToInt(t/pointSpacing) 的浮点除法误差
        accumulatedT = new float[pointCount];
        for (int i = 0; i < pointCount; i++)
            accumulatedT[i] = i * pointSpacing;
    }

    /// <summary>BoxCollider2D 四角转 polygon path 再采样</summary>
    void SampleBoxCollider(BoxCollider2D box)
    {
        Vector3 scl = transform.localScale;
        Vector2 half = Vector2.Scale(box.size * 0.5f, scl);
        Vector2 off = Vector2.Scale(box.offset, scl);

        // 顺时针四顶点
        Vector2[] path = new Vector2[4];
        path[0] = new Vector2( half.x,  half.y) + off; // 右上
        path[1] = new Vector2( half.x, -half.y) + off; // 右下
        path[2] = new Vector2(-half.x, -half.y) + off; // 左下
        path[3] = new Vector2(-half.x,  half.y) + off; // 左上
        pathVertices = path;

        // winding：以上顺序为 clockwise
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
            GetPointOnPath(path, t, out Vector2 pt, out Vector2 n);
            sampledPoints[i] = pt;
            sampledNormals[i] = n;
        }

        // 累距数组：消除 FloorToInt(t/pointSpacing) 的浮点除法误差
        accumulatedT = new float[pointCount];
        for (int i = 0; i < pointCount; i++)
            accumulatedT[i] = i * pointSpacing;
    }

    /// <summary>在 path 上距离 t 处取点 + 向外法线（基于 winding order）</summary>
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

                // 向外法线：左垂直 = 指向多边形外侧（clockwise 时）
                Vector2 edgeDir = (end - start).normalized;
                Vector2 leftNormal = new Vector2(-edgeDir.y, edgeDir.x);
                normal = polyClockwise ? leftNormal : -leftNormal;
                return;
            }
            accumulated += edgeLen;
        }

        // fallback
        point = path[path.Length - 1];
        normal = Vector2.up;
    }

    // ============ 统一表面接口 ============

    /// <summary>在 accumulatedT 中二分查找 t 所属的采样段索引（保证 O(logN) 且无浮点除法误差）</summary>
    int FindSampleIndex(float t)
    {
        int lo = 0, hi = accumulatedT.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (accumulatedT[mid] <= t)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo;
    }

    /// <summary>根据表面距离 t 获取世界坐标位置（含 outwardOffset 避免穿透实体 collider）</summary>
    public Vector2 GetSurfacePoint(float t)
    {
        if (isPolygon)
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
            Vector2 normal = GetSurfaceNormal(t); // 直接用边几何法线，一致
            return (Vector2)transform.position + local + normal * outwardOffset;
        }
        else
        {
            float angle = t / moveRadius;
            Vector2 outward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            return (Vector2)transform.position + outward * (moveRadius + outwardOffset);
        }
    }

    /// <summary>根据表面距离 t 获取向外法线（头朝向）</summary>
    public Vector2 GetSurfaceNormal(float t)
    {
        if (isPolygon)
        {
            t = WrapT(t);

            // ★ 直接用边几何算外法线，不依赖采样插值（采样顶点处法线有歧义）
            int edgeIdx = -1;
            for (int i = 0; i < pathVertices.Length; i++)
            {
                float edgeEnd = (i == pathVertices.Length - 1) ? totalPerimeter : edgeStartT[i + 1];
                if (t >= edgeStartT[i] && t < edgeEnd)
                {
                    edgeIdx = i;
                    break;
                }
            }
            if (edgeIdx < 0) edgeIdx = pathVertices.Length - 1; // t 恰等于 totalPerimeter

            Vector2 start = pathVertices[edgeIdx];
            Vector2 end = pathVertices[(edgeIdx + 1) % pathVertices.Length];
            Vector2 edgeDir = (end - start).normalized;
            Vector2 leftNormal = new Vector2(-edgeDir.y, edgeDir.x);
            return polyClockwise ? leftNormal : -leftNormal;
        }
        else
        {
            float angle = t / moveRadius;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    /// <summary>给定世界坐标，找到表面上最近点的 t 值（多边形用边线段投影，圆形用角度）</summary>
    public float FindClosestSurfaceT(Vector3 worldPos)
    {
        Vector2 local = worldPos - transform.position;

        if (isPolygon)
        {
            int bestEdge = 0;
            float bestDist = float.MaxValue;
            float bestEdgeT = 0f; // 投影在边上的参数 [0,1]

            for (int i = 0; i < pathVertices.Length; i++)
            {
                Vector2 a = pathVertices[i];
                Vector2 b = pathVertices[(i + 1) % pathVertices.Length];
                Vector2 ab = b - a;
                float abLenSq = ab.sqrMagnitude;

                if (abLenSq < 0.0001f) continue;

                // 投影 local 到线段 ab
                float t = Mathf.Clamp01(Vector2.Dot(local - a, ab) / abLenSq);
                Vector2 closest = a + t * ab;
                float dSq = (local - closest).sqrMagnitude;

                if (dSq < bestDist)
                {
                    bestDist = dSq;
                    bestEdge = i;
                    bestEdgeT = t;
                }
            }

            // 沿周长距离 = 边起点 t + 在边上的距离
            Vector2 edgeA = pathVertices[bestEdge];
            Vector2 edgeB = pathVertices[(bestEdge + 1) % pathVertices.Length];
            float edgeLen = Vector2.Distance(edgeA, edgeB);

            // 拐角偏置：避免精确落在顶点导致法线歧义（顶点法线属于上一条边而非进入边）
            float epsilon = Mathf.Min(edgeLen * 0.001f, 0.001f);
            if (bestEdgeT < epsilon) bestEdgeT = epsilon;
            else if (bestEdgeT > 1f - epsilon) bestEdgeT = 1f - epsilon;

            return edgeStartT[bestEdge] + bestEdgeT * edgeLen;
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
        float t = FindClosestSurfaceT(worldPos);
        return GetSurfacePoint(t);
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

    // ============ Gizmos 调试 ============

    void OnDrawGizmosSelected()
    {
        if (isPolygon && pathVertices != null && pathVertices.Length > 0)
        {
            // —— 多边形边 + 法线 ——
            for (int i = 0; i < pathVertices.Length; i++)
            {
                Vector2 a = (Vector2)transform.position + pathVertices[i];
                Vector2 b = (Vector2)transform.position + pathVertices[(i + 1) % pathVertices.Length];

                // 边线（青色）
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(a, b);

                // 边的中点
                Vector2 mid = (a + b) * 0.5f;

                // 边法线（黄色，0.5 长度）
                Vector2 edgeDir = (b - a).normalized;
                Vector2 leftNormal = new Vector2(-edgeDir.y, edgeDir.x);
                Vector2 outward = polyClockwise ? leftNormal : -leftNormal;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(mid, mid + outward * 0.5f);

                // 顶点小球（绿色）
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(a, 0.05f);
            }

            // —— 采样点 + 法线（每隔 4 个点画一个，避免太密）——
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
        else
        {
            // —— 圆形：画圆环 + 采样法线 ——
            float r = moveRadius;
            Gizmos.color = Color.cyan;
            Vector3 center = transform.position;
            int segs = 32;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * 2f * Mathf.PI / segs;
                float a1 = (i + 1) * 2f * Mathf.PI / segs;
                Vector2 p0 = center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * r;
                Vector2 p1 = center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                Gizmos.DrawLine(p0, p1);
            }

            // 法线（每隔 45° 画一根）
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
}
