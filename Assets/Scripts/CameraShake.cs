using Cinemachine;
using UnityEngine;

/// <summary>
/// 摄像头抖动 — Cinemachine Impulse 方案。
/// 挂在 Player 上，Inspector 拖引用 CinemachineImpulseSource。
/// 调用 Shake() / Shake(float) 即可触发抖动。
/// 抖动的形状、持续时间等参数全部在 ImpulseSource Inspector 上调，代码只负责触发。
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("抖动参数")]
    [Tooltip("Cinemachine Impulse Source 组件引用")]
    [SerializeField] CinemachineImpulseSource impulseSource;

    [Tooltip("默认抖动强度（Shake() 无参数时使用）")]
    [SerializeField] float defaultIntensity = 1f;

    void Awake()
    {
        // 如果没拖引用，尝试在同对象或子对象上自动找
        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    /// <summary>用默认强度触发抖动</summary>
    public void Shake()
    {
        Shake(defaultIntensity);
    }

    /// <summary>用指定强度触发抖动</summary>
    public void Shake(float intensity)
    {
        if (impulseSource == null) return;

        // GenerateImpulse 的参数就是抖动力度
        // 其他参数（信号形状、持续时间、衰减曲线）全在 ImpulseSource Inspector 上调
        impulseSource.GenerateImpulse(intensity);
    }
}
