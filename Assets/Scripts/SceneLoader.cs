using UnityEngine.SceneManagement;

/// <summary>
/// 极简场景切换。去掉 Mirror 依赖，直接调用 SceneManager。
/// 用法：SceneLoader.Go(GameScene.GamePlay);
/// </summary>
public enum GameScene
{
    Lobby,
    GamePlay
}

public static class SceneLoader
{
    public static void Go(GameScene scene)
    {
        SceneManager.LoadScene(ToName(scene));
    }

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
