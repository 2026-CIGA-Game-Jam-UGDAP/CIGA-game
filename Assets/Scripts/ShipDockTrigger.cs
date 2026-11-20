using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 飞船对接触发器。挂在 triggerDown / triggerUp 上。
/// 玩家持续停留在舱门区域后才视为就位，离开即清除。
/// </summary>
public class ShipDockTrigger : MonoBehaviour
{
    [Tooltip("场景中的 GameManager")]
    public GameManager gameManager;

    [Tooltip("等待队友提示（世界空间 UI），玩家就位后显示")]
    public GameObject waitingText;

    [Tooltip("出入口的半透明角色剪影，未就位且零件收齐时显示")]
    public GameObject dockGuide;
    [Tooltip("剪影上方指向站位的箭头")]
    public Transform guideArrow;

    const double RequiredHoldSeconds = 0.5;
    PlayerController candidate;
    double candidateSince;
    PlayerController dockedPlayer;
    bool shipLaunched;
    Collider2D dockCollider;
    readonly List<Collider2D> overlaps = new List<Collider2D>(8);
    Vector3 arrowRestPosition;
    Tween arrowTween;

    public bool HasPlayer => dockedPlayer != null;
    public PlayerController DockedPlayer => dockedPlayer;

    void Awake()
    {
        dockCollider = GetComponent<Collider2D>();
        if (guideArrow != null) arrowRestPosition = guideArrow.localPosition;
        if (dockGuide != null) dockGuide.SetActive(false);
    }

    void OnEnable()
    {
        RefreshWaitingText();
    }

    void OnDisable()
    {
        candidate = null;
        SetDockedPlayer(null);
        SetGuideVisible(false);
    }

    void Update()
    {
        RefreshDockGuide();
    }

    void OnDestroy()
    {
        arrowTween?.Kill();
    }

    void FixedUpdate()
    {
        RefreshDockedPlayer();
    }

    internal void RefreshDockedPlayer()
    {
        if (!isActiveAndEnabled || shipLaunched) return;

        // 按物理帧核对实际重叠，发船前也用同一份物理状态复核两扇门。
        PlayerController player = null;
        if (dockCollider != null && dockCollider.enabled)
        {
            int count = dockCollider.OverlapCollider(new ContactFilter2D().NoFilter(), overlaps);
            for (int i = 0; i < count; i++)
            {
                var pc = overlaps[i].GetComponentInParent<PlayerController>();
                if (pc == null || !pc.isActiveAndEnabled || pc.IsFlyingToAnchor) continue;

                if (player == null) player = pc;

                // 同一玩家可能有多个碰撞体，候选仍在区域内时继续计时。
                if (pc == candidate)
                {
                    player = pc;
                    break;
                }
            }
        }

        double now = Time.fixedTimeAsDouble;
        if (!ReferenceEquals(candidate, player))
        {
            candidate = player;
            candidateSince = now;
        }

        SetDockedPlayer(candidate != null && now - candidateSince >= RequiredHoldSeconds ? candidate : null);
    }

    void SetDockedPlayer(PlayerController player)
    {
        if (ReferenceEquals(dockedPlayer, player)) return;

        dockedPlayer = player;
        RefreshWaitingText();
    }

    /// <summary>刷新等待队友提示：本 Dock 有玩家时显示</summary>
    void RefreshWaitingText()
    {
        if (waitingText != null)
            waitingText.SetActive(!shipLaunched && HasPlayer);
        RefreshDockGuide();
    }

    void RefreshDockGuide()
    {
        bool show = isActiveAndEnabled && !shipLaunched && !HasPlayer &&
            gameManager != null && gameManager.ShouldShowDockGuides;
        SetGuideVisible(show);
    }

    void SetGuideVisible(bool show)
    {
        if (dockGuide == null || dockGuide.activeSelf == show) return;
        dockGuide.SetActive(show);

        arrowTween?.Kill();
        arrowTween = null;
        if (guideArrow == null) return;
        guideArrow.localPosition = arrowRestPosition;
        if (show)
            arrowTween = guideArrow.DOLocalMoveY(arrowRestPosition.y - 0.22f, 0.65f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>飞船已发射，停止响应进出</summary>
    public void MarkLaunched()
    {
        shipLaunched = true;
        if (waitingText != null)
            waitingText.SetActive(false);
        SetGuideVisible(false);
    }
}
