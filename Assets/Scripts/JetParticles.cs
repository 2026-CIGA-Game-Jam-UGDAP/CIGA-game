using UnityEngine;

/// <summary>
/// 喷气粒子效果。玩家喷气时播放，停止时关闭。
/// 放在玩家子对象上，拖入对应的 ParticleSystem。
/// </summary>
public class JetParticles : MonoBehaviour
{
    [Tooltip("要控制的喷气粒子系统")]
    public ParticleSystem particles;

    PlayerController playerCtrl;

    void Start()
    {
        playerCtrl = GetComponentInParent<PlayerController>();

        if (particles != null)
            particles.Stop();
    }

    void Update()
    {
        if (particles == null || playerCtrl == null) return;

        if (playerCtrl.IsJetting && !particles.isPlaying)
        {
            particles.Play();
        }
        else if (!playerCtrl.IsJetting && particles.isPlaying)
        {
            particles.Stop();
        }
    }
}
