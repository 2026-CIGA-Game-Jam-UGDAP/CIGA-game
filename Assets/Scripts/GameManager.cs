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
    [Tooltip("过关后加载的场景名")]
    public string nextSceneName = "Level2";
    [SerializeField] SceneVideo loadingVideo;
    [SerializeField] bool advanceAfterEnterDialogue;

    [Header("暂停菜单")]
    public PauseMenu pauseMenu;

    [Header("对话系统")]
    [Tooltip("关卡进入时自动播放的对话")]
    public DialogueSO enterDialogue;
    [Tooltip("（关卡0）任一玩家首次解除吸附后的提示")]
    public DialogueSO afterDetachDialogue;
    [Tooltip("（关卡0）同一玩家解除后重新吸附时播放的对话")]
    public DialogueSO afterAttachDialogue;
    [Tooltip("（关卡0）任一玩家首次喷气后播放的对话")]
    public DialogueSO afterJetpackDialogue;
    [Tooltip("（关卡0）任一玩家到达平台右侧后的喷气提示")]
    public DialogueSO afterExitDialogue;
    [Tooltip("（关卡0）玩家靠近出入口，提示需要同时站在标记区域")]
    public DialogueSO approachDockDialogue;
    [Tooltip("（关卡0）吸附第一颗星球并触碰能量后播放的对话")]
    public DialogueSO afterRechargeDialogue;
    [Tooltip("（关卡0）飞船进入主摄像机视野后播放的对话")]
    public DialogueSO afterRechargeGoToShipDialogue;
    [SerializeField] float tutorialPlatformRightX;
    [SerializeField] AnchorPoint tutorialFirstPlanet;
    [SerializeField] Camera tutorialCamera;
    [Tooltip("零件收齐后，任一玩家回到飞船时播放的双人对接引导（无零件的关卡直接在登船时播放）")]
    public DialogueSO afterReachShipDialogue;
    [Tooltip("收集完所有零件后播放的回船引导")]
    public DialogueSO afterCollectDialogue;
    [Tooltip("陨石撞击后播放的对话")]
    public DialogueSO afterMeteorDialogue;
    [Tooltip("过关后、切换场景前播放的对话")]
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
    bool hasWon;
    bool changingScene;

    // 对话调度状态
    bool enterDialogueReady;
    bool enterDialoguePlayed;
    bool detachDialogueTriggered;
    bool attachDialogueTriggered;
    bool jetpackDialogueTriggered;
    bool exitDialogueTriggered;
    bool approachDockDialoguePending;
    bool approachDockDialogueTriggered;
    bool rechargeDialogueTriggered;
    bool goToShipDialogueTriggered;
    bool reachShipDialogueTriggered;
    bool collectDialogueTriggered;
    bool meteorDialogueTriggered;

    // 能量教程状态（独立于对话链）
    bool energyTutorialPending;
    bool energyTutorialTriggered;

    // 玩家操作标志（其他脚本设置）
    bool jetpackUsed;
    bool player1Attached;
    bool player2Attached;
    [System.NonSerialized] public bool meteorImpactTriggered;
    bool player1Detached;
    bool player2Detached;
    bool landedOnFirstPlanet;
    bool touchedEnergy;

    /// <summary>开局初始化中，其他脚本读这个禁用输入/物理</summary>
    public static bool IsInitializing { get; private set; }

    /// <summary>全局单例，方便其他脚本调对话方法</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>收齐零件后显示飞船出入口引导，发船或重开时隐藏。</summary>
    public bool ShouldShowDockGuides => collectedGoals >= totalGoalPickups && !shipAnimating && !hasWon && !resetting;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
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
        {
            yield return fadeImage.DOFade(0f, fadeDuration).WaitForCompletion();
            fadeImage.raycastTarget = false;
        }

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
        if (tutorialFirstPlanet != null)
        {
            while ((player1 != null && player1.IsFlyingToAnchor) || (player2 != null && player2.IsFlyingToAnchor))
                yield return null;
        }
        enterDialogueReady = true;
    }

    void Update()
    {
        if (resetting || changingScene) return;

        // ESC 暂停菜单
        if (Input.GetKeyDown(KeyCode.Escape) && pauseMenu != null)
        {
            if (PauseMenu.IsOpen)
                pauseMenu.Close();
            else
                pauseMenu.Open();
        }

        if (PauseMenu.IsOpen) return;

        // ★ 暂时取消掉落死亡
        // if (player1 != null && player1.transform.position.y < deathY)
        //     ResetLevel();
        // if (player2 != null && player2.transform.position.y < deathY)
        //     ResetLevel();

        CheckDialogueTriggers();

        // 对话关闭后重新核对对接条件，已站好位置的玩家不需要退出再进入。
        CheckShipCondition();
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
        if (hasWon || resetting) return;
        resetting = true;
        StartCoroutine(ResetLevelAsync());
    }

    IEnumerator ResetLevelAsync()
    {
        // 1. 黑屏淡入
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = true;
            yield return fadeImage.DOFade(1f, fadeDuration).WaitForCompletion();
        }

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
        yield return null;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>检查飞船对接+零件收集条件</summary>
    void CheckShipCondition()
    {
        if (PauseMenu.IsOpen) return;
        if (shipAnimating || hasWon || resetting || collectedGoals < totalGoalPickups) return;

        if (dockTrigger1 == null || dockTrigger2 == null)
        {
            return;
        }
        if (!dockTrigger1.HasPlayer || !dockTrigger2.HasPlayer)
        {
            return;
        }

        // 两个 trigger 里必须是不同玩家
        if (dockTrigger1.DockedPlayer == dockTrigger2.DockedPlayer)
        {
            return;
        }

        // 最后一个零件被拾取时可能两人已经就位，仍需先完整显示本关引导。
        if (DialogueManager.Instance != null)
        {
            if (!enterDialogueReady || DialogueManager.IsActive) return;
            if (totalGoalPickups > 0 && afterCollectDialogue != null && !collectDialogueTriggered) return;
            if (afterReachShipDialogue != null && !reachShipDialogueTriggered) return;
        }

        Debug.Log("[Ship] 条件满足，启动飞船动画！");
        StartCoroutine(ShipLaunchSequence());
    }

    System.Collections.IEnumerator ShipLaunchSequence()
    {
        shipAnimating = true;

        // 触发飞船动画
        if (shipAnimator != null)
        {
            var progressLights = shipAnimator.GetComponent<ShipProgressLights>();
            if (progressLights != null)
                progressLights.CompleteLevel();

            shipAnimator.SetTrigger(shipAnimTrigger);
        }

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
        hasWon = true;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySceneTransition();

        if (endingDialogue != null && DialogueManager.Instance != null)
            DialogueManager.Instance.StartDialogue(endingDialogue, ContinueToNextScene);
        else
            ContinueToNextScene();
    }

    void ContinueToNextScene()
    {
        if (changingScene || string.IsNullOrEmpty(nextSceneName)) return;
        changingScene = true;
        if (loadingVideo == null)
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }
        StartCoroutine(LoadWithVideo());
    }

    IEnumerator LoadWithVideo()
    {
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        loadingVideo.Play();
        while (!loadingVideo.HasFrame && !loadingVideo.IsFinished)
            yield return null;

        var load = SceneManager.LoadSceneAsync(nextSceneName);
        if (load != null)
        {
            load.allowSceneActivation = false;
            while (!loadingVideo.IsFinished || load.progress < 0.9f)
                yield return null;
        }
        Time.timeScale = previousTimeScale;
        if (load != null) load.allowSceneActivation = true;
    }

    // ==================== 对话调度 ====================

    /// <summary>每帧检查条件，触发对应对话（顺序链式触发）</summary>
    void CheckDialogueTriggers()
    {
        if (shipAnimating || hasWon || changingScene || DialogueManager.Instance == null || DialogueManager.IsActive) return;
        if (!enterDialogueReady) return;

        // 1. 关卡进入对话（自动触发一次）
        if (!enterDialoguePlayed && enterDialogue != null)
        {
            enterDialoguePlayed = true;
            DialogueManager.Instance.StartDialogue(enterDialogue, advanceAfterEnterDialogue ? ContinueToNextScene : null);
            return;
        }

        // 2. 首次解除吸附对话（关卡0：一人脱离初始吸附后即可触发）
        if (enterDialoguePlayed && !detachDialogueTriggered &&
            (player1Detached || player2Detached) && afterDetachDialogue != null)
        {
            detachDialogueTriggered = true;
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

        // 到达平台右侧后，介绍喷气背包和按键。
        if (attachDialogueTriggered && !exitDialogueTriggered && afterExitDialogue != null &&
            ((player1 != null && player1.transform.position.x >= tutorialPlatformRightX) ||
             (player2 != null && player2.transform.position.x >= tutorialPlatformRightX)))
        {
            exitDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterExitDialogue);
            return;
        }

        if (exitDialogueTriggered && !jetpackDialogueTriggered && jetpackUsed && afterJetpackDialogue != null)
        {
            jetpackDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterJetpackDialogue);
            return;
        }

        // 关卡 0 的能量站提示排在喷气和耗能说明之后。
        if (energyTutorialPending && energyTutorialDialogue != null &&
            (tutorialFirstPlanet == null || jetpackDialogueTriggered))
        {
            energyTutorialPending = false;
            energyTutorialTriggered = true;
            if (tutorialFirstPlanet == null || !touchedEnergy)
            {
                DialogueManager.Instance.StartDialogue(energyTutorialDialogue);
                return;
            }
        }

        // 6.5 靠近出入口提示对话（关卡0：玩家靠近出入口时触发，提示同时站在标记区域）
        if (exitDialogueTriggered && !approachDockDialogueTriggered &&
            approachDockDialoguePending && approachDockDialogue != null)
        {
            approachDockDialoguePending = false;
            approachDockDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(approachDockDialogue);
            return;
        }

        // 两位玩家共享教学进度，吸附和拾取可以按任意顺序完成。
        if (jetpackDialogueTriggered && !rechargeDialogueTriggered &&
            landedOnFirstPlanet && touchedEnergy && afterRechargeDialogue != null)
        {
            rechargeDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterRechargeDialogue);
            return;
        }

        // 7.5 指引前往飞船对话（关卡0：补充能量后等飞船进入主摄像机视野）
        if (rechargeDialogueTriggered && !goToShipDialogueTriggered &&
            afterRechargeGoToShipDialogue != null && IsShipInView())
        {
            goToShipDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterRechargeGoToShipDialogue);
            return;
        }

        // 8. 各关收齐零件后先提示回到飞船，无需收集的教学关不弹此提示。
        if (!collectDialogueTriggered && IsCollectConditionMet() && afterCollectDialogue != null)
        {
            collectDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterCollectDialogue);
            return;
        }

        // 教学关等两人登船，收集关保留一人回船时的提示。
        bool readyToDock = totalGoalPickups <= 0 ||
            (collectedGoals >= totalGoalPickups && (afterCollectDialogue == null || collectDialogueTriggered));
        if (readyToDock && !reachShipDialogueTriggered && afterReachShipDialogue != null &&
            (tutorialFirstPlanet != null
                ? IsPlayerOnShip(player1) && IsPlayerOnShip(player2)
                : IsPlayerOnShip(player1) || IsPlayerOnShip(player2)))
        {
            reachShipDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterReachShipDialogue);
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
    /// 收齐零件即可提示回船；没有开场对话的关卡也能正常触发。
    /// </summary>
    bool IsCollectConditionMet()
    {
        return totalGoalPickups > 0 && collectedGoals >= totalGoalPickups &&
            (enterDialogue == null || enterDialoguePlayed);
    }

    bool IsShipInView()
    {
        if (tutorialCamera == null || shipAnchor == null) return false;
        Vector3 viewport = tutorialCamera.WorldToViewportPoint(shipAnchor.transform.position);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    /// <summary>当前吸附在飞船表面或已进入出入口，即视为登船。</summary>
    bool IsPlayerOnShip(PlayerController player)
    {
        if (player == null) return false;
        if ((dockTrigger1 != null && dockTrigger1.DockedPlayer == player) ||
            (dockTrigger2 != null && dockTrigger2.DockedPlayer == player)) return true;
        return shipAnchor != null && player.IsAnchored && ReferenceEquals(player.CurrentAnchor, shipAnchor);
    }

    public void OnPlayerDetached(int playerIndex)
    {
        if (playerIndex == 0) player1Detached = true;
        else player2Detached = true;
    }

    public void OnPlayerJetpacked()
    {
        jetpackUsed = true;
    }

    public void OnPlayerAttached(int playerIndex, Component anchor)
    {
        if (playerIndex == 0) player1Attached |= player1Detached;
        else player2Attached |= player2Detached;
        landedOnFirstPlanet |= anchor == tutorialFirstPlanet;
    }

    public void OnEnergyPickup()
    {
        touchedEnergy = true;
    }

    /// <summary>MeteorManager 或其他脚本调用：陨石撞击发生</summary>
    public void OnMeteorImpact()
    {
        meteorImpactTriggered = true;
    }

    /// <summary>出入口 Trigger 调用：玩家靠近出入口，提示需要同时站在标记区域</summary>
    public void TriggerApproachDockDialogue()
    {
        if (approachDockDialogueTriggered) return;
        approachDockDialoguePending = true;
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
