using UnityEngine;

/// <summary>
/// 弹性出生 — 从 scale=0 弹到 scale=1 带 overshoot。
/// Inspector 里画 AnimationCurve 调手感，调用 Play() 触发。
/// </summary>
public class SpawnBounce : MonoBehaviour
{
    [Header("弹性参数")]
    [SerializeField] AnimationCurve bounceCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.4f, 1.15f),
        new Keyframe(0.7f, 0.95f),
        new Keyframe(1f, 1f)
    );
    [SerializeField] float duration = 0.6f;

    float timer = -1f;
    Vector3 initialScale;

    void Awake()
    {
        initialScale = transform.localScale;
    }

    /// <summary>触发弹性出生动画</summary>
    public void Play()
    {
        transform.localScale = Vector3.zero;
        timer = 0f;
    }

    void Update()
    {
        if (timer < 0f) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);
        float curve = bounceCurve.Evaluate(t);
        transform.localScale = initialScale * curve;

        if (t >= 1f)
        {
            transform.localScale = initialScale;
            timer = -1f;
        }
    }
}
