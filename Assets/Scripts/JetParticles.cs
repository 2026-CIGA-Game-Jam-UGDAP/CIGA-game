using UnityEngine;

/// <summary>
/// 喷气/行走粒子效果，根据状态切换颜色：
/// - 自由飞行喷气 → 蓝色火焰
/// - 圆形锚点（月球）行走 → 土黄色尘土
/// - 多边形锚点（火箭）→ 关闭
/// </summary>
public class JetParticles : MonoBehaviour
{
    [Tooltip("要控制的粒子系统")]
    public ParticleSystem particles;

    [Tooltip("自由飞行喷气颜色（蓝色火焰）")]
    public Color jetColor = new Color(0.29f, 0.56f, 1f);
    [Tooltip("圆形锚点（月球）行走尘土颜色")]
    public Color moonDustColor = new Color(0.76f, 0.6f, 0.33f);

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

        bool isAnchored = playerCtrl.IsAnchored;
        bool isJetting = playerCtrl.IsJetting;

        // 多边形锚点：不显示粒子
        bool isPolyAnchor = isAnchored && playerCtrl.CurrentAnchor is PolyAnchorPoint;
        if (isPolyAnchor)
        {
            if (particles.isPlaying)
                particles.Stop();
            return;
        }

        bool isCircleAnchor = isAnchored && playerCtrl.CurrentAnchor is AnchorPoint;

        bool shouldPlay;
        Color color;

        if (!isAnchored && isJetting)
        {
            // 自由飞行喷气 → 蓝色火焰
            shouldPlay = true;
            color = jetColor;
        }
        else if (isCircleAnchor && isJetting)
        {
            // 圆形锚点（月球）行走 → 土黄色尘土
            shouldPlay = true;
            color = moonDustColor;
        }
        else
        {
            shouldPlay = false;
            color = Color.white; // 不会用到
        }

        if (shouldPlay)
        {
            var main = particles.main;
            main.startColor = color;

            if (!particles.isPlaying)
                particles.Play();
        }
        else
        {
            if (particles.isPlaying)
                particles.Stop();
        }
    }
}
