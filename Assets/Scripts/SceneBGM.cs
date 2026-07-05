using UnityEngine;

/// <summary>
/// 每个场景放一个 GameObject 挂此组件，拖入该场景专属的背景音乐。
/// 启动时自动通知 AudioManager 切换 BGM。
/// </summary>
public class SceneBGM : MonoBehaviour
{
    [Tooltip("本场景的背景音乐")]
    public AudioClip bgmClip;

    void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBGM(bgmClip);
    }
}
