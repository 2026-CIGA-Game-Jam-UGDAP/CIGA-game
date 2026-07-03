using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 锚点：靠近按吸附键 → 飞到锚点中心，在范围内可自由移动。
/// 再按一次 → 脱离。一个锚点同时只能吸附一个玩家。
/// P1=F, P2=RightShift。
/// </summary>
public class AnchorPoint : MonoBehaviour
{
    [Header("吸附参数")]
    [Tooltip("飞向锚点的移动速度")]
    [SerializeField] float snapSpeed = 12f;
    [Tooltip("吸附后玩家可移动半径。0=自动读取 CircleCollider2D.radius")]
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
    PlayerController anchoredPlayer;

    void Awake()
    {
        // 没手动设 moveRadius → 自动读非 trigger 的 CircleCollider2D（星球本体大小）
        if (moveRadius <= 0f)
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
            // 回退：所有 collider 都是 trigger → 读第一个
            if (moveRadius <= 0f)
            {
                var col = GetComponent<CircleCollider2D>();
                if (col != null) moveRadius = col.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
            }
        }
    }

    void Start()
    {
        if (indicator != null)
            indicator.color = freeColor;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // ★ 用 GetComponentInParent，适配 Collider 在子物体上的情况
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
        // 清理 null 引用
        for (int i = playersInRange.Count - 1; i >= 0; i--)
        {
            if (playersInRange[i] == null)
                playersInRange.RemoveAt(i);
        }

        for (int i = 0; i < playersInRange.Count; i++)
        {
            var pc = playersInRange[i];
            if (pc == null) continue;

            KeyCode key = pc.playerIndex == 0 ? p1SnapKey : p2SnapKey;
            if (!Input.GetKeyDown(key)) continue;

            if (pc.IsAnchored)
            {
                // 已吸附 → 脱离
                pc.DetachFromAnchor();
                anchoredPlayer = null;
                if (indicator != null) indicator.color = freeColor;
            }
            else if (anchoredPlayer == null)
            {
                // 未吸附 + 锚点空闲 → 吸附
                pc.AttachToAnchor(this, snapSpeed);
                anchoredPlayer = pc;
                if (indicator != null) indicator.color = occupiedColor;
            }
        }
    }
}
