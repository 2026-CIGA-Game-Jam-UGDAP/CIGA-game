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
    [Tooltip("两个玩家都喷气后播放的对话")]
    public DialogueSO afterJetpackDialogue;
    [Tooltip("两个玩家都吸附后播放的对话")]
    public DialogueSO afterAttachDialogue;
    [Tooltip("陨石撞击后播放的对话")]
    public DialogueSO afterMeteorDialogue;

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
    bool jetpackDialogueTriggered;
    bool attachDialogueTriggered;
    bool meteorDialogueTriggered;

    // 玩家操作标志（其他脚本设置）
    [System.NonSerialized] public bool player1Jetpacked;
    [System.NonSerialized] public bool player2Jetpacked;
    [System.NonSerialized] public bool player1Attached;
    [System.NonSerialized] public bool player2Attached;
    [System.NonSerialized] public bool meteorImpactTriggered;

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

        // ★ 自动吸附到飞船
        if (autoSnapToShip && shipAnchor != null)
        {
            if (player1 != null && !player1.IsAnchored)
                player1.AttachToAnchor(shipAnchor, autoSnapSpeed);
            if (player2 != null && !player2.IsAnchored)
                player2.AttachToAnchor(shipAnchor, autoSnapSpeed);
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

        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
    }

    // ==================== 对话调度 ====================

    /// <summary>每帧检查条件，触发对应对话</summary>
    void CheckDialogueTriggers()
    {
        if (DialogueManager.Instance == null || DialogueManager.IsActive) return;
        if (!enterDialogueReady) return;

        // 1. 关卡进入对话（自动触发一次）
        if (!enterDialoguePlayed && enterDialogue != null)
        {
            enterDialoguePlayed = true;
            DialogueManager.Instance.StartDialogue(enterDialogue);
            return;
        }

        // 2. 喷气背包对话（两个人都喷气后触发）
        if (enterDialoguePlayed && !jetpackDialogueTriggered &&
            player1Jetpacked && player2Jetpacked && afterJetpackDialogue != null)
        {
            jetpackDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterJetpackDialogue);
            return;
        }

        // 3. 吸附对话（两个人都吸附后触发）
        if (jetpackDialogueTriggered && !attachDialogueTriggered &&
            player1Attached && player2Attached && afterAttachDialogue != null)
        {
            attachDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterAttachDialogue);
            return;
        }

        // 4. 陨石对话
        if (!meteorDialogueTriggered && meteorImpactTriggered && afterMeteorDialogue != null)
        {
            meteorDialogueTriggered = true;
            DialogueManager.Instance.StartDialogue(afterMeteorDialogue);
        }
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

    /// <summary>MeteorManager 或其他脚本调用：陨石撞击发生</summary>
    public void OnMeteorImpact()
    {
        meteorImpactTriggered = true;
    }
}
