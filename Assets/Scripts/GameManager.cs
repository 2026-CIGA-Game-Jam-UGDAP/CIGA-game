using UnityEngine;
using System.Collections;
using DG.Tweening;
using UnityEngine.UI;
using Obi;

/// <summary>
/// 游戏流程管理：检测过关、掉落重来、绳索断裂。
/// 挂在一个空 GameObject 上。
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("玩家")]
    public PlayerController player1;
    public PlayerController player2;

    [Header("生成点")]
    public Transform spawnPoint1;
    public Transform spawnPoint2;

    [Header("绳索")]
    public RopeController ropeController;

    [Header("失败条件")]
    [Tooltip("玩家掉到这个 Y 值以下即死亡")]
    public float deathY = -10f;

    [Header("通关零件")]
    [Tooltip("场景中 Goal 类型 Pickup 的总数")]
    public int totalGoalPickups = 3;
    int collectedGoals;

    [Header("重来效果")]
    public Image fadeImage;          // 全屏黑色 Image（DoTween 控制 alpha）
    public float fadeDuration = 0.3f;

    bool p1AtGoal;
    bool p2AtGoal;
    bool resetting;

    Rigidbody2D rb1;
    Rigidbody2D rb2;

    void Start()
    {
        rb1 = player1 != null ? player1.GetComponent<Rigidbody2D>() : null;
        rb2 = player2 != null ? player2.GetComponent<Rigidbody2D>() : null;

        // 确保初始 fade 透明
        if (fadeImage != null)
            fadeImage.color = new Color(0, 0, 0, 0);
    }

    void Update()
    {
        if (resetting) return;

        // 掉落死亡
        if (player1 != null && player1.transform.position.y < deathY)
            ResetLevel();
        if (player2 != null && player2.transform.position.y < deathY)
            ResetLevel();
    }

    /// <summary>终点 Trigger 调用。playerIndex: 0=P1, 1=P2</summary>
    public void OnPlayerEnterGoal(int playerIndex)
    {
        if (playerIndex == 0) p1AtGoal = true;
        if (playerIndex == 1) p2AtGoal = true;

        if (p1AtGoal && p2AtGoal)
            Victory();
    }

    public void OnPlayerExitGoal(int playerIndex)
    {
        if (playerIndex == 0) p1AtGoal = false;
        if (playerIndex == 1) p2AtGoal = false;
    }

    /// <summary>Goal 类型 Pickup 被拾取时调用</summary>
    public void OnGoalPickupCollected()
    {
        collectedGoals++;
        Debug.Log($"[GameManager] 零件 {collectedGoals}/{totalGoalPickups}");
        if (collectedGoals >= totalGoalPickups)
            Victory();
    }

    /// <summary>绳索断裂时 Obi 调用</summary>
    public void OnRopeBreak()
    {
        if (!resetting)
            ResetLevel();
    }

    void ResetLevel()
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

        p1AtGoal = false;
        p2AtGoal = false;
        collectedGoals = 0;

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

    void Victory()
    {
        Debug.Log("🎉 两人都到终点，过关！");
        // TODO: 过关动画 / 下一关
    }
}
