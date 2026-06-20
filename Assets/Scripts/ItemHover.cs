using UnityEngine;

/// <summary>
/// 道具悬浮 + 旋转 — 地上的道具/拾取物持续上下浮动和自转。
/// 挂在道具 GameObject 上即可，零调用，全自动。
/// </summary>
public class ItemHover : MonoBehaviour
{
    [Header("浮动参数")]
    [SerializeField] float floatAmplitude = 0.3f;
    [SerializeField] float floatFrequency = 2f;

    [Header("旋转参数")]
    [SerializeField] float rotateSpeed = 90f;
    [SerializeField] bool rotateOnZ = true;  // 俯视角旋转 Z 轴 = 水平旋转

    Vector3 startPos;

    void Awake()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 上下浮动：sin 波
        Vector3 pos = startPos;
        pos.y += Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = pos;

        // 自转
        Vector3 rot = transform.eulerAngles;
        if (rotateOnZ)
            rot.z += rotateSpeed * Time.deltaTime;
        else
            rot.y += rotateSpeed * Time.deltaTime;
        transform.eulerAngles = rot;
    }
}
