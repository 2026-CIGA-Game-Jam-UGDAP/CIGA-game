using UnityEngine;

/// <summary>
/// Sprite 闪白 — 受击/选中/复活的高频反馈。
/// 挂在有 SpriteRenderer/MeshRenderer 的对象上，调用 Flash() 触发闪白。
/// 用 Material 的 _Color 属性做颜色覆盖，闪完自动恢复原色。
/// </summary>
public class SpriteFlash : MonoBehaviour
{
    [Header("闪白参数")]
    [SerializeField] Color flashColor = Color.white;
    [SerializeField] float flashDuration = 0.1f;
    [SerializeField] int flashCount = 1;

    Renderer rend;
    Color originalColor;
    float timer;
    int currentFlash;
    bool flashing;

    void Awake()
    {
        rend = GetComponentInChildren<Renderer>();
    }

    /// <summary>触发闪白（默认颜色和次数）</summary>
    public void Flash()
    {
        Flash(flashColor, flashCount);
    }

    /// <summary>触发闪白（指定颜色和次数）</summary>
    public void Flash(Color color, int count)
    {
        if (rend == null) return;

        // 记住原始颜色
        originalColor = rend.material.color;
        flashColor = color;
        flashCount = count;
        currentFlash = 0;
        timer = 0f;
        flashing = true;

        // 立刻显示闪白颜色
        rend.material.color = flashColor;
    }

    void Update()
    {
        if (!flashing) return;

        timer += Time.deltaTime;

        // 每个闪白周期 = flashDuration
        // 半周期白，半周期原色
        float halfCycle = flashDuration * 0.5f;
        float fullCycle = flashDuration;

        if (timer < halfCycle)
        {
            rend.material.color = flashColor;
        }
        else if (timer < fullCycle)
        {
            rend.material.color = originalColor;
        }
        else
        {
            // 一个完整周期结束
            timer = 0f;
            currentFlash++;

            if (currentFlash >= flashCount)
            {
                // 所有闪烁完成，恢复原色
                rend.material.color = originalColor;
                flashing = false;
            }
            else
            {
                // 开始下一个闪烁周期
                rend.material.color = flashColor;
            }
        }
    }
}
