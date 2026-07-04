using UnityEngine.SceneManagement;

/// <summary>
/// 极简场景切换。去掉 Mirror 依赖，直接调用 SceneManager。
/// 用法：SceneLoader.Go(GameScene.GamePlay);
/// </summary>
public enum GameScene
{
    Lobby,
    Level0,
    Level1,
    Level2,
    Level3
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
            case GameScene.Lobby:  return "Lobby";
            case GameScene.Level0: return "Level0";
            case GameScene.Level1: return "Level1";
            case GameScene.Level2: return "Level2";
            case GameScene.Level3: return "Level3";
            default:               return "Lobby";
        }
    }
}
