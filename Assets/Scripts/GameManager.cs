using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Obi;

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

    [Header("重来效果")]
    [Tooltip("全屏黑色遮罩 Image（用于淡入淡出）")]
    public Image fadeImage;
    [Tooltip("黑屏淡入淡出的持续时间（秒）")]
    public float fadeDuration = 0.3f;

    bool resetting;
    bool shipAnimating;

    Rigidbody2D rb1;
    Rigidbody2D rb2;

    /// <summary>开局初始化中，其他脚本读这个禁用输入/物理</summary>
    public static bool IsInitializing { get; private set; }

    void Start()
    {
        rb1 = player1 != null ? player1.GetComponent<Rigidbody2D>() : null;
        rb2 = player2 != null ? player2.GetComponent<Rigidbody2D>() : null;

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

        // 2. 禁用绳索 + 等 solver 停稳
        ObiRope rope = ropeController != null ? ropeController.rope : null;
        if (rope != null && spawnPoint1 != null && spawnPoint2 != null)
        {
            rope.enabled = false;
            yield return new WaitForFixedUpdate();

            // 3. 把绳 transform 对齐到出生点
            rope.transform.position = spawnPoint1.position;
            Vector3 dir = (spawnPoint2.position - spawnPoint1.position).normalized;
            if (dir != Vector3.zero)
                rope.transform.right = dir;

            // 4. 直接改每个粒子的本地位置：插值分布在两出生点之间
            int count = rope.UsedParticles;
            Vector3[] localPositions = rope.positions;
            for (int i = 0; i < count && i < localPositions.Length; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                Vector3 worldPos = Vector3.Lerp(spawnPoint1.position, spawnPoint2.position, t);
                localPositions[i] = rope.transform.InverseTransformPoint(worldPos);
            }

            // 5. 清零粒子速度
            for (int i = 0; i < rope.velocities.Length; i++)
                rope.velocities[i] = Vector3.zero;
            for (int i = 0; i < rope.angularVelocities.Length; i++)
                rope.angularVelocities[i] = Vector3.zero;

            // ★ 等 solver 用新 transform + 新本地位置刷新粒子世界位置
            yield return new WaitForFixedUpdate();
        }

        // 6. 瞬移玩家 + 归零速度
        if (player1 != null && spawnPoint1 != null)
            player1.transform.position = spawnPoint1.position;
        if (player2 != null && spawnPoint2 != null)
            player2.transform.position = spawnPoint2.position;
        if (rb1 != null) rb1.velocity = Vector2.zero;
        if (rb2 != null) rb2.velocity = Vector2.zero;

        shipAnimating = false;
        collectedGoals = 0;
        if (partHUD != null)
            partHUD.SetDisplayQuiet(0, totalGoalPickups);

        // 7. 重新启用绳索
        if (rope != null)
        {
            rope.enabled = true;
            ropeController.ResetRope();
        }

        // 8. 黑屏淡出
        if (fadeImage != null)
            yield return fadeImage.DOFade(0f, fadeDuration).WaitForCompletion();

        resetting = false;
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
}
