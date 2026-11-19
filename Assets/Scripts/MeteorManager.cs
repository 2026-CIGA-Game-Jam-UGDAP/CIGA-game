using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// 陨石生成控制 — 按时间间隔在玩家右上生成陨石。
/// 挂场景空 GameObject 上，拖好引用就能跑。
/// </summary>
public class MeteorManager : MonoBehaviour
{
    [Header("玩家引用")]
    [Tooltip("不拖的话自动从 GameManager 拿")]
    [SerializeField] PlayerController player1;
    [SerializeField] PlayerController player2;

    [Header("预制体")]
    [SerializeField] GameObject meteorPrefab;
    [SerializeField] GameObject warningPrefab;

    [Header("生成参数")]
    [Tooltip("两次陨石之间的秒数")]
    [SerializeField] float spawnInterval = 3f;
    [Tooltip("预警持续秒数")]
    [SerializeField] float warningDuration = 0.6f;
    [Tooltip("陨石飞行速度")]
    [SerializeField] float meteorSpeed = 8f;
    [Tooltip("弹开力道 (喷气 jetForce=10, 所以 50-100 是 5-10x)")]
    [SerializeField] float knockbackForce = 80f;

    [Header("生成位置")]
    [Tooltip("相对玩家右上方的基准偏移")]
    [SerializeField] Vector2 spawnOffsetBase = new Vector2(10f, 15f);
    [Tooltip("随机散布半径")]
    [SerializeField] float spawnSpread = 4f;

    [Header("方向散布")]
    [Tooltip("方向偏离玩家目标区域的角度（度），0 = 精准经过玩家")]
    [SerializeField] float angleSpread = 15f;

    [Header("陨石流")]
    [SerializeField] bool enableMeteorStorm;
    [SerializeField, Min(0.05f)] float stormSpawnInterval = 0.25f;
    [SerializeField, Min(1f)] float stormSpeedMultiplier = 2f;
    [SerializeField] GameObject stormWarningBanner;
    [SerializeField] TMP_Text stormWarningText;

    const float StormPeriod = 50f;
    const float StormDuration = 5f;
    const float StormWarningDuration = 3f;

    float spawnTimer;
    float timeUntilStorm = StormPeriod;
    float stormSpawnTimer;
    int displayedWarningSeconds;

    void Start()
    {
        // 没拖引用：从 GameManager 拿
        if (player1 == null || player2 == null)
        {
            GameManager gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                if (player1 == null) player1 = gm.player1;
                if (player2 == null) player2 = gm.player2;
            }
        }

        // 初始随机偏移，避免开局同时砸
        spawnTimer = Random.Range(1f, spawnInterval);
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;
        spawnTimer -= deltaTime;
        if (spawnTimer <= 0f)
        {
            spawnTimer = spawnInterval;
            TrySpawnMeteor(meteorSpeed, true);
        }

        if (enableMeteorStorm)
            UpdateMeteorStorm(deltaTime);
    }

    void UpdateMeteorStorm(float deltaTime)
    {
        timeUntilStorm -= deltaTime;
        if (timeUntilStorm <= -StormDuration)
        {
            timeUntilStorm += StormPeriod;
            stormSpawnTimer = 0f;
        }

        bool showWarning = deltaTime > 0f && timeUntilStorm > 0f && timeUntilStorm <= StormWarningDuration;
        if (stormWarningBanner != null && stormWarningBanner.activeSelf != showWarning)
            stormWarningBanner.SetActive(showWarning);

        if (showWarning && stormWarningText != null)
        {
            int seconds = Mathf.CeilToInt(timeUntilStorm);
            if (displayedWarningSeconds != seconds)
            {
                displayedWarningSeconds = seconds;
                stormWarningText.text = $"陨石流还有{seconds}s来袭";
            }
        }

        if (deltaTime <= 0f || timeUntilStorm > 0f) return;

        stormSpawnTimer -= deltaTime;
        if (stormSpawnTimer <= 0f)
        {
            stormSpawnTimer += stormSpawnInterval;
            TrySpawnMeteor(meteorSpeed * stormSpeedMultiplier, false);
        }
    }

    void TrySpawnMeteor(float speed, bool warnAtSpawnPoint)
    {
        // 随机选一个玩家
        PlayerController target = PickRandomPlayer();
        if (target == null) return;

        Vector2 playerPos = target.transform.position;

        // 生成位置：玩家右上 + 随机散布
        Vector2 spawnPos = playerPos + spawnOffsetBase + Random.insideUnitCircle * spawnSpread;

        // 目标方向：指向玩家附近（加角度散布）
        Vector2 toPlayer = (playerPos + Random.insideUnitCircle * 2f) - spawnPos;
        Vector2 baseDir = toPlayer.normalized;
        float angle = Random.Range(-angleSpread, angleSpread);
        Vector2 meteorDir = Quaternion.Euler(0f, 0f, angle) * baseDir;

        // 先生成预警
        if (warnAtSpawnPoint && warningPrefab != null)
        {
            GameObject warning = Instantiate(warningPrefab, spawnPos, Quaternion.identity);
            // warning 自己播动画，到时间后由协程销毁
            StartCoroutine(SpawnMeteorAfterWarning(spawnPos, meteorDir, warning, speed));
        }
        else
        {
            SpawnMeteor(spawnPos, meteorDir, speed);
        }
    }

    IEnumerator SpawnMeteorAfterWarning(Vector2 pos, Vector2 dir, GameObject warning, float speed)
    {
        yield return new WaitForSeconds(warningDuration);

        if (warning != null)
            Destroy(warning);

        SpawnMeteor(pos, dir, speed);
    }

    void SpawnMeteor(Vector2 pos, Vector2 dir, float speed)
    {
        if (meteorPrefab == null)
            return;

        GameObject meteor = Instantiate(meteorPrefab, pos, Quaternion.identity);
        Meteor m = meteor.GetComponent<Meteor>();
        if (m != null)
        {
            m.direction = dir;
            m.speed = speed;
            m.knockbackForce = knockbackForce;
        }
    }

    PlayerController PickRandomPlayer()
    {
        bool p1Valid = player1 != null;
        bool p2Valid = player2 != null;

        if (!p1Valid && !p2Valid) return null;
        if (!p1Valid) return player2;
        if (!p2Valid) return player1;

        return Random.value < 0.5f ? player1 : player2;
    }
}
