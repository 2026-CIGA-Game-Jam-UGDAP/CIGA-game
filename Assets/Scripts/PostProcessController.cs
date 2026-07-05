using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 全屏后处理动态控制 — 陨石砸中 Bloom 爆发等。
/// 挂在一个场景 GameObject 上（Setup 脚本会自动添加）。
/// 用法：PostProcessController.Flash(intensity, duration)
/// </summary>
public class PostProcessController : MonoBehaviour
{
    public static PostProcessController Instance { get; private set; }

    [Header("默认闪白参数")]
    [SerializeField] float defaultFlashIntensity = 4f;
    [SerializeField] float defaultFlashDuration = 0.15f;

    Volume flashVolume;
    Bloom bloom;
    float flashTarget;
    float flashTimer;
    float flashDur;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetupFlashVolume();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void SetupFlashVolume()
    {
        var go = new GameObject("_FlashVolume");
        go.transform.SetParent(transform);

        flashVolume = go.AddComponent<Volume>();
        flashVolume.isGlobal = true;
        flashVolume.weight = 0f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        bloom = profile.Add<Bloom>();
        bloom.active = true;
        bloom.threshold.Override(0f);   // 闪白期间所有亮度都发光
        bloom.intensity.Override(0f);
        bloom.scatter.Override(0.7f);
        flashVolume.profile = profile;
    }

    /// <summary>全屏 Bloom 爆发。外部调用。</summary>
    public static void Flash(float intensity = -1f, float duration = -1f)
    {
        if (Instance == null) return;
        float i = intensity < 0f ? Instance.defaultFlashIntensity : intensity;
        float d = duration < 0f ? Instance.defaultFlashDuration : duration;
        Instance.TriggerFlash(i, d);
    }

    void TriggerFlash(float intensity, float duration)
    {
        flashTarget = intensity;
        flashDur = duration;
        flashTimer = duration;
        flashVolume.weight = 1f;
        bloom.intensity.Override(intensity);
    }

    void Update()
    {
        if (flashTimer <= 0f) return;

        flashTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(1f - flashTimer / flashDur);
        float current = Mathf.Lerp(flashTarget, 0f, t);
        bloom.intensity.Override(current);

        if (flashTimer <= 0f)
        {
            bloom.intensity.Override(0f);
            flashVolume.weight = 0f;
        }
    }
}
