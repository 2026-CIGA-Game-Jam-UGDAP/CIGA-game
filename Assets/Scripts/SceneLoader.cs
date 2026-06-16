using Mirror;
using UnityEngine;

// 用 GameScene 而不是 Scene：避免和 UnityEngine.SceneManagement.Scene 同名导致 C# 歧义。
// 新增场景时：① 这里加一项 ② 下方 ToName 加映射 ③ Unity Build Settings 里加入该场景。
public enum GameScene
{
    Lobby,
    GamePlay
}

/// <summary>
/// 极简场景切换门面。任何端调用 Go() 即可，内部自动判断 Server / Client。
/// 用法：SceneLoader.Go(GameScene.GamePlay);
/// </summary>
public static class SceneLoader
{
    /// <summary>
    /// 切换到目标场景。
    /// Server / Host 直接切；远程客户端自动发请求给 Server，由 Server 统一切（所有人一起切）。
    /// </summary>
    public static void Go(GameScene scene)
    {
        if (NetworkManager.singleton is SceneNetworkManager net)
        {
            net.RequestChangeScene(scene);
        }
        else
        {
            Debug.LogError("[SceneLoader] 场景里没有 SceneNetworkManager。请用它替换默认 NetworkManager。");
        }
    }

    /// <summary>枚举 → 场景名（Mirror 的 ServerChangeScene 收 string）。</summary>
    public static string ToName(GameScene scene)
    {
        switch (scene)
        {
            case GameScene.Lobby:    return "Lobby";
            case GameScene.GamePlay: return "GamePlay";
            default:                 return "Lobby";
        }
    }
}
