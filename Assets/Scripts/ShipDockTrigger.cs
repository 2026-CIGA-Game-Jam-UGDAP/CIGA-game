using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 飞船对接触发器。挂在 triggerDown / triggerUp 上。
/// 根据实际重叠的玩家通知 GameManager 检查对接条件，显示等待队友提示。
/// 不锁定玩家移动——玩家可自由进出。
/// </summary>
public class ShipDockTrigger : MonoBehaviour
{
    [Tooltip("场景中的 GameManager")]
    public GameManager gameManager;

    [Tooltip("等待队友提示（世界空间 UI），有玩家时显示")]
    public GameObject waitingText;

    [Tooltip("出入口的半透明角色剪影，未就位且零件收齐时显示")]
    public GameObject dockGuide;
    [Tooltip("剪影上方指向站位的箭头")]
    public Transform guideArrow;

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

    void OnTriggerEnter2D(Collider2D other)
    {
        RefreshDockedPlayer();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        RefreshDockedPlayer();
    }

    void RefreshDockedPlayer()
    {
        if (!isActiveAndEnabled || shipLaunched) return;

        // OnEnable 时物理世界可能还没同步，Enter/Exit 也可能因启用或多个碰撞体漏记。
        // 每个物理帧核对实际重叠，不需要玩家退出后重新进入。
        PlayerController player = null;
        if (dockCollider != null && dockCollider.enabled)
        {
            int count = dockCollider.OverlapCollider(new ContactFilter2D().NoFilter(), overlaps);
            for (int i = 0; i < count; i++)
            {
                var pc = overlaps[i].GetComponentInParent<PlayerController>();
                if (pc == null) continue;

                if (player == null) player = pc;

                // 同一玩家可能有多个碰撞体；只要仍有一个在区域内，就保留对接状态。
                if (pc == dockedPlayer)
                {
                    player = pc;
                    break;
                }
            }
        }

        SetDockedPlayer(player);
    }

    void SetDockedPlayer(PlayerController player)
    {
        if (ReferenceEquals(dockedPlayer, player)) return;

        dockedPlayer = player;
        RefreshWaitingText();
        if (gameManager != null)
        {
            if (HasPlayer)
                gameManager.OnPlayerDocked();
            else
                gameManager.OnPlayerUndocked();
        }
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
