using System;
using Mirror;
using UnityEngine;

public class SceneNetworkManager : NetworkManager
{
    // 防重入：切换进行中忽略新请求。
    bool transitioning;

    // ---- 请求入口（SceneLoader.Go 调到这里）----

    /// <summary>Server/Host 直接切；远程客户端发消息请求 Server 切。</summary>
    public void RequestChangeScene(GameScene scene)
    {
        if (NetworkServer.active)
        {
            ChangeSceneOnServer(scene);
        }
        else if (NetworkClient.active)
        {
            NetworkClient.Send(new ChangeSceneMessage { sceneIndex = (int)scene });
        }
        else
        {
            Debug.LogWarning("[SceneLoader] 尚未连接，无法切换场景。");
        }
    }

    void ChangeSceneOnServer(GameScene scene)
    {
        if (transitioning) return;
        transitioning = true;
        ServerChangeScene(SceneLoader.ToName(scene));
    }

    // ---- 服务器：注册客户端→服务器的场景请求 ----

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<ChangeSceneMessage>(OnClientRequestScene);
    }

    void OnClientRequestScene(NetworkConnectionToClient conn, ChangeSceneMessage msg)
    {
        if (Enum.IsDefined(typeof(GameScene), msg.sceneIndex))
            ChangeSceneOnServer((GameScene)msg.sceneIndex);
        else
            Debug.LogWarning($"[SceneLoader] 收到非法场景请求 index={msg.sceneIndex}");
    }

    // 服务器端新场景加载完成 → 复位防重入。
    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        transitioning = false;
    }
}

/// <summary>客户端→服务器的场景切换请求。</summary>
public struct ChangeSceneMessage : NetworkMessage
{
    public int sceneIndex;
}
