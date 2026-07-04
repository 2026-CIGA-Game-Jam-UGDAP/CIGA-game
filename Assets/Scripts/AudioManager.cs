using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 全局音效管理器。Singleton + DontDestroyOnLoad。
/// 每个场景都放一个（挂 AudioManager 预制体或空 GameObject），重复的自动销毁。
///
/// 2D 音效（UI类）：场景转换、捡能量、捡零件、全收集 —— 全局等响度播放
/// 3D 音效（空间化）：吸附、脚步、推进器 —— 有空间位置
/// 推进器为循环音效，其他为 one-shot
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ============ 2D 音效 Clip（UI 类，全局等响度） ============
    [Header("2D UI 音效")]
    public AudioClip sceneTransitionClip;
    public AudioClip energyPickupClip;
    public AudioClip partPickupClip;
    public AudioClip allCollectedClip;

    // ============ 3D 音效 Clip（空间化） ============
    [Header("3D 空间音效")]
    public AudioClip thrusterLoopClip;
    public AudioClip footstepMoonClip;
    public AudioClip footstepRocketClip;
    public AudioClip adsorptionClip;

    [Header("3D 音源池")]
    [Tooltip("3D one-shot 音源数量（脚步、吸附等并发上限）")]
    public int poolSize = 6;

    // ---- 内部 ----
    AudioSource uiSource;
    AudioSource[] pool3D;
    int poolIndex;

    // ---- 推进器跟踪 ----
    Dictionary<Transform, AudioSource> thrusterSources = new Dictionary<Transform, AudioSource>();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 2D 音源：挂在自身
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.spatialBlend = 0f;
        uiSource.playOnAwake = false;

        // 3D 音源池：每个是子 GameObject
        pool3D = new AudioSource[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            GameObject child = new GameObject("AudioSource3D_" + i);
            child.transform.SetParent(transform);
            AudioSource src = child.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.playOnAwake = false;
            src.minDistance = 1f;
            src.maxDistance = 30f;
            pool3D[i] = src;
        }
    }

    // ==================== 2D One-Shot ====================

    public void PlaySceneTransition()
    {
        if (sceneTransitionClip != null)
            uiSource.PlayOneShot(sceneTransitionClip);
    }

    public void PlayEnergyPickup()
    {
        if (energyPickupClip != null)
            uiSource.PlayOneShot(energyPickupClip);
    }

    public void PlayPartPickup()
    {
        if (partPickupClip != null)
            uiSource.PlayOneShot(partPickupClip);
    }

    public void PlayAllCollected()
    {
        if (allCollectedClip != null)
            uiSource.PlayOneShot(allCollectedClip);
    }

    // ==================== 3D One-Shot ====================

    /// <summary>在指定世界位置播放吸附音效</summary>
    public void PlayAdsorption(Vector3 worldPos)
    {
        Play3DOneShot(adsorptionClip, worldPos);
    }

    /// <summary>在指定位置播放脚步声。isMoon=true 月球表面，false=火箭表面</summary>
    public void PlayFootstep(Vector3 worldPos, bool isMoon)
    {
        AudioClip clip = isMoon ? footstepMoonClip : footstepRocketClip;
        Play3DOneShot(clip, worldPos);
    }

    void Play3DOneShot(AudioClip clip, Vector3 worldPos)
    {
        if (clip == null) return;

        AudioSource src = pool3D[poolIndex];
        src.transform.position = worldPos;
        src.PlayOneShot(clip);

        poolIndex = (poolIndex + 1) % pool3D.Length;
    }

    // ==================== 3D 循环推进器 ====================

    /// <summary>开始推进器循环音效。传入玩家 Transform 和该玩家身上的 AudioSource</summary>
    public void StartThruster(Transform player, AudioSource source)
    {
        if (thrusterLoopClip == null || source == null) return;

        // 避免重复启动
        if (thrusterSources.ContainsKey(player)) return;

        source.clip = thrusterLoopClip;
        source.loop = true;
        source.spatialBlend = 1f;
        source.Play();
        thrusterSources[player] = source;
    }

    /// <summary>停止推进器循环音效</summary>
    public void StopThruster(Transform player)
    {
        if (!thrusterSources.TryGetValue(player, out AudioSource src)) return;

        src.Stop();
        src.clip = null;
        thrusterSources.Remove(player);
    }

    // ==================== 脚步接力（Animation Event 调用） ====================

    /// <summary>
    /// 由 PlayerController.OnFootstep() 接力调用。
    /// 在玩家位置播放脚步声。
    /// </summary>
    public void PlayFootstepFromPlayer(Transform player, bool isMoon)
    {
        PlayFootstep(player.position, isMoon);
    }
}
