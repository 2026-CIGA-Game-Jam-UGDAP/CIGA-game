using UnityEngine;

/// <summary>
/// 摄像头抖动 — 直接操作 MainCamera Transform，零依赖。
/// 挂在 Player 上，调用 Shake(intensity) 即可。
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("抖动参数")]
    [Tooltip("默认抖动强度")]
    [SerializeField] float defaultIntensity = 1f;

    [Tooltip("默认抖动持续时间（秒）")]
    [SerializeField] float defaultDuration = 0.15f;

    Transform camTransform;
    Vector3 prevShakeOffset;
    float shakeTimer;
    float shakeIntensity;

    void Start()
    {
        var cam = Camera.main;
        if (cam != null)
            camTransform = cam.transform;
    }

    public void Shake() => Shake(defaultIntensity);

    public void Shake(float intensity)
    {
        if (camTransform == null)
        {
            var cam = Camera.main;
            if (cam != null) camTransform = cam.transform;
            else return;
        }

        shakeIntensity = intensity;
        shakeTimer = defaultDuration;
    }

    void LateUpdate()
    {
        // 先还原上一帧抖动偏移，避免漂移
        if (prevShakeOffset != Vector3.zero)
            camTransform.position -= prevShakeOffset;

        if (shakeTimer <= 0f)
        {
            prevShakeOffset = Vector3.zero;
            return;
        }

        float decay = shakeTimer / defaultDuration;
        float currentIntensity = shakeIntensity * decay;
        prevShakeOffset = (Vector3)(Random.insideUnitCircle * currentIntensity * 0.3f);

        camTransform.position += prevShakeOffset;
        shakeTimer -= Time.deltaTime;
    }
}
