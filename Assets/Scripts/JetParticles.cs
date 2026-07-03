using UnityEngine;

/// <summary>
/// 喷气粒子效果。移动/跳跃时播放，停止时关闭。
/// 放在玩家子对象上，拖入对应的 ParticleSystem。
/// </summary>
public class JetParticles : MonoBehaviour
{
    [Tooltip("要控制的喷气粒子系统")]
    public ParticleSystem particles;

    [Tooltip("喷气相对于玩家的偏移方向（太空喷气通常是背后/脚下）")]
    public Vector2 emitDirection = Vector2.down;

    Rigidbody2D playerRb;
    PlayerController playerCtrl;

    void Start()
    {
        playerRb = GetComponentInParent<Rigidbody2D>();
        playerCtrl = GetComponentInParent<PlayerController>();

        if (particles != null)
            particles.Stop();
    }

    void Update()
    {
        if (particles == null || playerRb == null) return;

        bool moving = Mathf.Abs(playerRb.velocity.x) > 0.5f;
        bool jumping = playerRb.velocity.y > 0.5f;

        if ((moving || jumping) && !particles.isPlaying)
        {
            particles.Play();
        }
        else if (!moving && !jumping && particles.isPlaying)
        {
            particles.Stop();
        }
    }
}
