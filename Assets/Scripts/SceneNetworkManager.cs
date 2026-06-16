using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 自定义 NetworkManager，承载场景切换模块的全部逻辑：
/// 1) 接收客户端→服务器的场景切换请求（单条 NetworkMessage，点对点，不是事件系统）。
/// 2) 重写场景回调，驱动黑屏 Fade 过渡（掩盖切换时的卡顿/白屏）。
///
/// 用法：把它挂到场景里替换掉默认 NetworkManager（删掉原 NetworkManager 组件即可）。
/// 调用方一律走 SceneLoader.Go(GameScene.XXX)。
/// </summary>
public class SceneNetworkManager : NetworkManager
{
    [Tooltip("黑屏淡入/淡出时长（秒）。手感在这里调。")]
    public float fadeDuration = 0.3f;

    // 防重入：切换进行中忽略新请求（只在 Server 端判断，因为 ServerChangeScene 在 Server 执行）。
    bool transitioning;

    // 运行时创建的过渡层（DontDestroyOnLoad，跨场景存活）。
    CanvasGroup fadeGroup;

    // ---- 请求入口（SceneLoader.Go 调到这里）----

    /// <summary>Server/Host 直接切；远程客户端发消息请求 Server 切。</summary>
    public void RequestChangeScene(GameScene scene)
    {
        if (NetworkServer.active)
        {
            // Host（server+client）或纯 Server：直接切，不走网络请求。
            ChangeSceneOnServer(scene);
        }
        else if (NetworkClient.active)
        {
            // 远程客户端：发单条请求给 Server。
            NetworkClient.Send(new ChangeSceneMessage { sceneIndex = (int)scene });
        }
        else
        {
            Debug.LogWarning("[SceneLoader] 尚未连接，无法切换场景。");
        }
    }

    void ChangeSceneOnServer(GameScene scene)
    {
        if (transitioning) return;          // 防重入：连点两次"开始"不会触发两次切换
        transitioning = true;
        ServerChangeScene(SceneLoader.ToName(scene));
    }

    // ---- 服务器：注册客户端→服务器的场景请求 ----

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<ChangeSceneMessage>(OnClientRequestScene);
    }

    // 注意：当前任何已连接客户端都能触发切场景（Game Jam 默认信任客户端）。
    // 若要限制（如只允许 Host 开始），在这里加 conn 身份判断即可。
    void OnClientRequestScene(NetworkConnectionToClient conn, ChangeSceneMessage msg)
    {
        if (Enum.IsDefined(typeof(GameScene), msg.sceneIndex))
            ChangeSceneOnServer((GameScene)msg.sceneIndex);
        else
            Debug.LogWarning($"[SceneLoader] 收到非法场景请求 index={msg.sceneIndex}");
    }

    // ---- 场景回调：驱动 Fade ----

    // 客户端开始加载新场景 → 淡入变黑（掩盖加载卡顿）。customHandling 保持 false，Mirror 照常自动加载。
    public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
    {
        base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
        StartCoroutine(FadeIn());
    }

    // 客户端新场景加载完成 → 淡出恢复。
    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();   // 必须：become ready + add player
        StartCoroutine(FadeOut());
    }

    // 服务器端新场景加载完成 → 复位防重入。
    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        transitioning = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureFadeCanvas();   // 只有客户端需要过渡视觉（纯 dedicated server 不需要）
    }

    // ---- 黑屏 Fade 视觉（运行时创建，零 prefab 配置）----

    void EnsureFadeCanvas()
    {
        if (fadeGroup != null) return;

        var go = new GameObject("SceneFadeCanvas");
        DontDestroyOnLoad(go);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        var image = go.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;
        var rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        fadeGroup = go.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;            // 初始透明、不挡操作
        fadeGroup.blocksRaycasts = false;
    }

    IEnumerator FadeIn()
    {
        EnsureFadeCanvas();
        if (fadeGroup != null) fadeGroup.blocksRaycasts = true;   // 切换中挡住点击
        yield return FadeRoutine(0f, 1f);
    }

    IEnumerator FadeOut()
    {
        EnsureFadeCanvas();
        yield return FadeRoutine(1f, 0f);
        if (fadeGroup != null) fadeGroup.blocksRaycasts = false;
    }

    IEnumerator FadeRoutine(float from, float to)
    {
        if (fadeGroup == null) yield break;
        float dur = fadeDuration > 0f ? fadeDuration : 0.01f;
        float t = 0f;
        fadeGroup.alpha = from;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;   // 不受 timeScale 影响（切换时可能有暂停）
            fadeGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            yield return null;
        }
        fadeGroup.alpha = to;
    }
}

/// <summary>客户端→服务器的场景切换请求（单条点对点消息，非事件系统/路由器）。</summary>
public struct ChangeSceneMessage : NetworkMessage
{
    public int sceneIndex;
}
