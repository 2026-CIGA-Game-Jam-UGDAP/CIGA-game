using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏流程管理：检测过关、掉落重来、绳索断裂。
/// 挂在一个空 GameObject 上。
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("玩家")]
    [Tooltip("玩家 1 的 PlayerController 引用")]
    public PlayerController player1;
    [Tooltip("玩家 2 的 PlayerController 引用")]
    public PlayerController player2;

    [Header("生成点")]
    [Tooltip("玩家 1 的出生/重生位置")]
    public Transform spawnPoint1;
    [Tooltip("玩家 2 的出生/重生位置")]
    public Transform spawnPoint2;

    [Header("绳索")]
    [Tooltip("绳索控制器引用")]
    public RopeController ropeController;

    [Header("失败条件")]
    [Tooltip("玩家掉到这个 Y 值以下即死亡重来")]
    public float deathY = -10f;

    [Header("通关零件")]
    [Tooltip("场景中 Goal 类型 Pickup 的总数（需捡够这个数才能过关）")]
    public int totalGoalPickups = 5;
    int collectedGoals;

    [Header("零件 HUD")]
    [Tooltip("右上角零件收集 UI，拖引用")]
    public PartCollectionUI partHUD;

    [Header("自动吸附")]
    [Tooltip("开局时自动将玩家吸附到飞船表面（每关可开关）")]
    public bool autoSnapToShip = true;
    [Tooltip("飞船的 PolyAnchorPoint 组件")]
    public PolyAnchorPoint shipAnchor;
    [Tooltip("自动吸附飞行速度")]
    public float autoSnapSpeed = 15f;

    [Header("飞船对接")]
    [Tooltip("飞船的 Animator 组件（动画 Trigger 名在下方配置）")]
    public Animator shipAnimator;
    [Tooltip("两个对接触发器（triggerDown / triggerUp）")]
    public ShipDockTrigger dockTrigger1;
    public ShipDockTrigger dockTrigger2;
    [Tooltip("触发飞船动画的 Animator Trigger 名")]
    public string shipAnimTrigger = "Play";
    [Tooltip("飞船动画播放时间（秒），播完后过关")]
    public float shipAnimDuration = 2f;

    [Header("下一关")]
    [Tooltip("过关后加载的场景名，Level1→Level2→Level3→Lobby")]
    public string nextSceneName = "Level2";

    [Header("暂停菜单")]
    public PauseMenu pauseMenu;

    [Header("对话系统")]
    [Tooltip("关卡进入时自动播放的对话")]
    public DialogueSO enterDialogue;
    [Tooltip("（关卡0）两个玩家都解除吸附后播放的对话")]
    public DialogueSO afterDetachDialogue;
    [Tooltip("（关卡0/1）两个玩家都吸附后播放的对话")]
    public DialogueSO afterAttachDialogue;
    [Tooltip("（关卡0）两个玩家再次解除吸附后播放的对话（喷气背包教程）")]
    public DialogueSO afterDetach2Dialogue;
    [Tooltip("两个玩家都喷气后播放的对话")]
    public DialogueSO afterJetpackDialogue;
    [Tooltip("（关卡0）玩家到达外部平台后播放的对话")]
    public DialogueSO afterExitDialogue;
    [Tooltip("（关卡0）两个玩家都补充能量后播放的对话")]
    public DialogueSO afterRechargeDialogue;
    [Tooltip("（关卡0）补充能量后，指引前往飞船的对话")]
    public DialogueSO afterRechargeGoToShipDialogue;
    [Tooltip("（关卡0）两个玩家都到达飞船后播放的对话")]
    public DialogueSO afterReachShipDialogue;
    [Tooltip("（关卡1+）收集完所有零件后播放的对话")]
    public DialogueSO afterCollectDialogue;
    [Tooltip("陨石撞击后播放的对话")]
    public DialogueSO afterMeteorDialogue;
    [Tooltip("结局对话（成功后播放，onComplete 挂场景切换）")]
    public DialogueSO endingDialogue;

    [Header("能量说明")]
    [Tooltip("能量站教程对话，独立于对话链")]
    public DialogueSO energyTutorialDialogue;

    [Header("重来效果")]
    [Tooltip("全屏黑色遮罩 Image（用于淡入淡出）")]
    public Image fadeImage;
    [Tooltip("黑屏淡入淡出的持续时间（秒）")]
    public float fadeDuration = 0.3f;

    bool resetting;
    bool shipAnimating;

    // 对话调度状态
    bool enterDialogueReady;
    bool enterDialoguePlayed;
    bool detachDialogueTriggered;
    bool attachDialogueTriggered;
    bool detach2DialogueTriggered;
    bool jetpackDialogueTriggered;
    bool exitDialogueTriggered;
    bool rechargeDialogueTriggered;
    bool goToShipDialogueTriggered;
    bool reachShipDialogueTriggered;
    bool collectDialogueTriggered;
    bool meteorDialogueTriggered;

    // 能量教程状态（独立于对话链）
    bool energyTutorialPending;
    bool energyTutorialTriggered;

    // 玩家操作标志（其他脚本设置）
    [System.NonSerialized] public bool player1Jetpacked;
    [System.NonSerialized] public bool player2Jetpacked;
    [System.NonSerialized] public bool player1Attached;
    [System.NonSerialized] public bool player2Attached;
    [System.NonSerialized] public bool meteorImpactTriggered;
    [System.NonSerialized] public bool player1Recharged;
    [System.NonSerialized] public bool player2Recharged;
    [System.NonSerialized] public bool player1ReachedShip;
    [System.NonSerialized] public bool player2ReachedShip;
    [System.NonSerialized] public bool playersExitedToPlatform;

    // 玩家解除吸附计数（关卡0区分第一次和第二次解除）
    int player1DetachCount;
    int player2DetachCount;

    /// <summary>开局初始化中，其他脚本读这个禁用输入/物理</summary>
    public static bool IsInitializing { get; private set; }

    /// <summary>全局单例，方便其他脚本调对话方法</summary>
    public static GameManager Instance { get; private set; }

    void Start()
    {
        Instance = this;

        // 初始化 HUD
        if (partHUD != null)
            partHUD.SetDisplayQuiet(0, totalGoalPickups);

        // 开局黑屏，等绳子就位后淡出
        if (fadeImage != null)
            fadeImage.color = Color.black;

        StartCoroutine(InitSequence());
    }

    System.Collections.IEnumerator InitSequence()
    {
        IsInitializing = true;

        // 等 RopeController.Start 跑完（生成粒子→冻结玩家→Pin→0.2s settle→解冻）
        yield return new WaitForSeconds(0.5f);

        // 多等一帧让 solver 稳定
        yield return new WaitForFixedUpdate();

        IsInitializing = false;

        if (fadeImage != null)
            yield return fadeImage.DOFade(0f, fadeDuration).WaitForCompletion();

        // ★ 自动吸附到最近的锚点（不再只吸飞船）
        if (autoSnapToShip)
        {
            if (player1 != null && !player1.IsAnchored)
            {
                var nearest = FindNearestAnchor(player1.transform.position);
                if (nearest != null)
                    AttachToNearest(player1, nearest);
            }
            if (player2 != null && !player2.IsAnchored)
            {
                var nearest = FindNearestAnchor(player2.transform.position);
                if (nearest != null)
                    AttachToNearest(player2, nearest);
            }
        }

        // ★ 等一帧再允许对话触发（避免跟淡入动画抢）
        yield return null;
        enterDialogueReady = true;
    }

    void Update()
    {
        if (resetting) return;

        // ESC 暂停菜单
        if (Input.GetKeyDown(KeyCode.Escape) && pauseMenu != null)
        {
            if (pauseMenu.gameObject.activeSelf)
                pauseMenu.Close();
            else
                pauseMenu.Open();
        }

        // ★ 暂时取消掉落死亡
        // if (player1 != null && player1.transform.position.y < deathY)
        //     ResetLevel();
        // if (player2 != null && player2.transform.position.y < deathY)
        //     ResetLevel();

        CheckDialogueTriggers();
    }

    /// <summary>ShipDockTrigger 调用：玩家进入对接区</summary>
    public void OnPlayerDocked()
    {
        CheckShipCondition();
    }

    /// <summary>ShipDockTrigger 调用：玩家离开对接区</summary>
    public void OnPlayerUndocked()
    {
        // 离开时不需要额外处理，CheckShipCondition 会在下次进入/收集时再判
    }

    /// <summary>Goal 类型 Pickup 被拾取时调用</summary>
    public void OnGoalPickupCollected()
    {
        collectedGoals++;

        if (partHUD != null)
            partHUD.UpdateDisplay(collectedGoals, totalGoalPickups);

        // 全部零件收集完毕
        if (collectedGoals >= totalGoalPickups && AudioManager.Instance != null)
            AudioManager.Instance.PlayAllCollected();

        CheckShipCondition();
    }

    /// <summary>绳索断裂时 Obi 调用 — 暂时禁用重开</summary>
    public void OnRopeBreak()
    {
        // ★ 暂时禁用重开逻辑
        // if (!resetting)
        //     ResetLevel();
    }

    public void ResetLevel()
    {
        if (resetting) return;
        resetting = true;
        StartCoroutine(ResetLevelAsync());
    }

    IEnumerator ResetLevelAsync()
    {
        // 1. 黑屏淡入
        if (fadeImage != null)
            yield return fadeImage.DOFade(1f, fadeDuration).WaitForCompletion();

        // 2. 完全重载场景：销毁所有对象，Obi + 物理从零初始化
        //    Start() → InitSequence() 会自然处理黑屏→绳子就位→淡出
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>完全重开：延迟一帧让 UI 事件退出，再重载场景</summary>
    public void RestartGame()
    {
        StartCoroutine(DoRestart());
    }

    IEnumerator DoRestart()
    {
        // 等帧末退出 UI 事件上下文，让 Obi 自然销毁
        yield return new WaitForEndOfFrame();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>检查飞船对接+零件收集条件</summary>
    void CheckShipCondition()
    {
        if (shipAnimating)
        {
            Debug.Log("[Ship] shipAnimating=true，跳过检查");
            return;
        }
        if (collectedGoals < totalGoalPickups)
        {
            Debug.Log($"[Ship] 零件不足: {collectedGoals}/{totalGoalPickups}");
            return;
        }

        if (dockTrigger1 == null || dockTrigger2 == null)
        {
            Debug.Log($"[Ship] dockTrigger 未全部赋值: 1={dockTrigger1 != null} 2={dockTrigger2 != null}");
            return;
        }
        if (!dockTrigger1.HasPlayer || !dockTrigger2.HasPlayer)
        {
            Debug.Log($"[Ship] 玩家未全部 dock: 1={dockTrigger1.HasPlayer} 2={dockTrigger2.HasPlayer}");
            return;
        }

        // 两个 trigger 里必须是不同玩家
        if (dockTrigger1.DockedPlayer == dockTrigger2.DockedPlayer)
        {
            Debug.Log("[Ship] 同一个玩家在两个 trigger 中，等待不同玩家");
            return;
        }

        Debug.Log("[Ship] 条件满足，启动飞船动画！");
        StartCoroutine(ShipLaunchSequence());
    }

    System.Collections.IEnumerator ShipLaunchSequence()
    {
        shipAnimating = true;

        // 触发飞船动画
        if (shipAnimator != null)
            shipAnimator.SetTrigger(shipAnimTrigger);

        // 等动画播完
        yield return new WaitForSeconds(shipAnimDuration);

        // 标记已发射，阻止重复触发
        if (dockTrigger1 != null)
            dockTrigger1.MarkLaunched();
        if (dockTrigger2 != null)
            dockTrigger2.MarkLaunched();

        // 关闭两个对接触发器
        if (dockTrigger1 != null)
            dockTrigger1.gameObject.SetActive(false);
        if (dockTrigger2 != null)
            dockTrigger2.gameObject.SetActive(false);

        Victory();
    }

    void Victory()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySceneTransition();

        // 有结局对话先播，onComplete 里挂场景切换；没有则直接切
        if (endingDialogue != null && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(endingDialogue);
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    // ==================== 对话调度 ====================

    /// <summary>每帧检查条件，触发对应对话（顺序链式触发）</summary>
    void CheckDialogueTriggers()
    {
        if (DialogueManager.Instance == null || DialogueManager.IsActive) return;
        if (!enterDialogueReady) return;

        // 能量教程对话：独立于链，待触发时在当前对话空隙中弹出
        if (energyTutorialPending && energyTutorialDialogue != null)
        {
            energyTutorialPending = false;
            energyTutorialTriggered = true;
            DialogueManager.Instance.StartDialogue(energyTutorialDialogue);
            return;
        }

        // 1. 关卡进入对话（自动触发一次）
        if (!enterDialoguePlayed && enterDialogue != null)
        {
            enterDialoguePlayed = true;
            DialogueManager.Instance.StartDialogue(enterDialogue);
            return;
        }

        // 2. 首次解除吸附对话（关卡0：一人脱离初始吸附后即可触发）
        if (enterDialoguePlayed && !detachDialogueTriggered &&
            (player1DetachCount >= 1 || player2DetachCount >= 1) && afterDetachDialogue != null)
        {
            detachDialogueTriggered = true;
            DialogueManager.Instance.allowActionKeyAdvance = true;
            DialogueManager.Instance.StartDialogue(afterDetachDialogue);
            return;
        }

        // 3. 吸附对话（一人吸附后即可触发）
        if (detachDialogueTriggered && !attachDialogueTriggered &&
            (player1Attached || player2Attached) && afterAttachDialogue != null)
        {
            attachDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterAttachDialogue);
            return;
        }

        // 4. 二次解除吸附对话（关卡0：一人再次解除吸附后即可触发喷气背包教程）
        if (attachDialogueTriggered && !detach2DialogueTriggered &&
            (player1DetachCount >= 2 || player2DetachCount >= 2) && afterDetach2Dialogue != null)
        {
            detach2DialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterDetach2Dialogue);
            return;
        }

        // 5. 喷气背包对话（一人喷气后即可触发）
        if (detach2DialogueTriggered && !jetpackDialogueTriggered &&
            (player1Jetpacked || player2Jetpacked) && afterJetpackDialogue != null)
        {
            jetpackDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterJetpackDialogue);
            return;
        }

        // 6. 到达外部平台对话（关卡0）
        if (jetpackDialogueTriggered && !exitDialogueTriggered &&
            playersExitedToPlatform && afterExitDialogue != null)
        {
            exitDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterExitDialogue);
            return;
        }

        // 7. 补充能量对话（关卡0：一人补充能量后即可触发）
        if (exitDialogueTriggered && !rechargeDialogueTriggered &&
            (player1Recharged || player2Recharged) && afterRechargeDialogue != null)
        {
            rechargeDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterRechargeDialogue);
            return;
        }

        // 7.5 指引前往飞船对话（关卡0：补充能量对话结束后自动弹出）
        if (rechargeDialogueTriggered && !goToShipDialogueTriggered &&
            afterRechargeGoToShipDialogue != null)
        {
            goToShipDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterRechargeGoToShipDialogue);
            return;
        }

        // 8. 到达飞船对话（关卡0）
        if (goToShipDialogueTriggeredOrSkipped() && !reachShipDialogueTriggered &&
            player1ReachedShip && player2ReachedShip && afterReachShipDialogue != null)
        {
            reachShipDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterReachShipDialogue);
            return;
        }

        // 9. 收集完零件对话（关卡1+：部分关卡没有关卡0流程，靠前置 trigger 已默认通过跳到这里）
        if (!collectDialogueTriggered && IsCollectConditionMet() && afterCollectDialogue != null)
        {
            collectDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterCollectDialogue);
            return;
        }

        // 10. 陨石对话（随时可触发，不依赖前置链）
        if (!meteorDialogueTriggered && meteorImpactTriggered && afterMeteorDialogue != null)
        {
            meteorDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterMeteorDialogue);
        }
    }

    /// <summary>
    /// 检查是否满足收集完零件触发条件：
    /// 对于有关卡0流程的关卡（rechargeDialogueTriggered=true），需要前置链走完；
    /// 对于其他关卡，只需要 enterDialoguePlayed 即可（如果有关卡0字段没填则默认通过）。
    /// </summary>
    bool IsCollectConditionMet()
    {
        // 如果场景中根本没填 afterCollectDialogue，别触发
        if (afterCollectDialogue == null) return false;

        // 如果填了关卡0的对话字段，说明这是有关卡0教学链的关卡，需要链走完
        bool hasLevel0Flow = afterDetachDialogue != null || afterRechargeDialogue != null;

        if (hasLevel0Flow)
        {
            // 有关卡0链：需要 recharge 对话已触发（或跳过） + 零件全部收集
            return (rechargeDialogueTriggered || rechargeDialogueTriggeredIfNoAsset()) &&
                   collectedGoals >= totalGoalPickups;
        }
        else
        {
            // 没有关卡0链（如 Level1+）：enter 对话播完 + 零件全部收集
            return enterDialoguePlayed && collectedGoals >= totalGoalPickups;
        }
    }

    /// <summary>如果场景中没填 afterRechargeDialogue 资产，视为已触发</summary>
    bool rechargeDialogueTriggeredIfNoAsset()
    {
        return afterRechargeDialogue == null || rechargeDialogueTriggered;
    }

    /// <summary>如果场景中没填 afterRechargeGoToShipDialogue 资产，视为已触发</summary>
    bool goToShipDialogueTriggeredOrSkipped()
    {
        return afterRechargeGoToShipDialogue == null || goToShipDialogueTriggered;
    }

    /// <summary>PlayerController 调用：玩家解除了吸附</summary>
    public void OnPlayerDetached(int playerIndex)
    {
        if (playerIndex == 0) player1DetachCount++;
        else player2DetachCount++;
    }

    /// <summary>PlayerController 调用：玩家使用了喷气背包</summary>
    public void OnPlayerJetpacked(int playerIndex)
    {
        if (playerIndex == 0) player1Jetpacked = true;
        else player2Jetpacked = true;
    }

    /// <summary>PlayerController 调用：玩家吸附到了表面</summary>
    public void OnPlayerAttached(int playerIndex)
    {
        if (playerIndex == 0) player1Attached = true;
        else player2Attached = true;
    }

    /// <summary>能量补给区 Trigger 调用：玩家补充了能量</summary>
    public void OnPlayerRecharged(int playerIndex)
    {
        if (playerIndex == 0) player1Recharged = true;
        else player2Recharged = true;
    }

    /// <summary>飞船对接区 Trigger 调用：玩家到达飞船</summary>
    public void OnPlayerReachedShip(int playerIndex)
    {
        if (playerIndex == 0) player1ReachedShip = true;
        else player2ReachedShip = true;
    }

    /// <summary>外部平台 Trigger 调用：玩家到达太空站外部平台</summary>
    public void OnPlayersExitedToPlatform()
    {
        playersExitedToPlatform = true;
    }

    /// <summary>MeteorManager 或其他脚本调用：陨石撞击发生</summary>
    public void OnMeteorImpact()
    {
        meteorImpactTriggered = true;
    }

    /// <summary>EnergyTutorialTrigger 调用：玩家进入能量站范围，设置待触发标记</summary>
    public void TriggerEnergyTutorial()
    {
        if (energyTutorialTriggered) return;
        energyTutorialPending = true;
    }

    // ==================== 最近锚点查找 ====================

    /// <summary>在所有锚点中找离 pos 最近的（返回 Component，调用方需 cast）</summary>
    Component FindNearestAnchor(Vector3 pos)
    {
        Component best = null;
        float bestDist = float.MaxValue;

        foreach (var a in FindObjectsOfType<AnchorPoint>())
        {
            var cp = a.GetClosestSurfacePoint(pos);
            float d = ((Vector2)pos - cp).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = a; }
        }
        foreach (var a in FindObjectsOfType<PolyAnchorPoint>())
        {
            var cp = a.GetClosestSurfacePoint(pos);
            float d = ((Vector2)pos - cp).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = a; }
        }

        return best;
    }

    /// <summary>根据锚点类型分发到正确的 AttachToAnchor 重载</summary>
    void AttachToNearest(PlayerController player, Component anchor)
    {
        if (anchor is AnchorPoint ap)
            player.AttachToAnchor(ap, autoSnapSpeed);
        else if (anchor is PolyAnchorPoint pap)
            player.AttachToAnchor(pap, autoSnapSpeed);
    }
}
