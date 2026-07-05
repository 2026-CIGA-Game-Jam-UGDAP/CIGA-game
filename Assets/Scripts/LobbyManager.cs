using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Lobby 主菜单。开始游戏 + 退出，带淡入淡出。
/// 挂在 Canvas 的 LobbyUI GameObject 上。
/// </summary>
public class LobbyManager : MonoBehaviour
{
    public Button btnStart;
    public Button btnQuit;
    public Image fadeImage;
    public float fadeDuration = 0.3f;

    void Start()
    {
        btnStart.onClick.AddListener(StartGame);
        btnQuit.onClick.AddListener(QuitGame);

        // 开局黑屏淡出
        if (fadeImage != null)
        {
            fadeImage.color = Color.black;
            fadeImage.DOFade(0f, fadeDuration)
                .OnComplete(() => fadeImage.raycastTarget = false);
        }
    }

    void StartGame()
    {
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = true;
            fadeImage.DOFade(1f, fadeDuration)
                .OnComplete(() => SceneLoader.Go(GameScene.Level0));
        }
        else
        {
            SceneLoader.Go(GameScene.Level0);
        }
    }

    void QuitGame()
    {
        Application.Quit();
    }
}
