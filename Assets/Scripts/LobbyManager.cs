using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

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

    [SerializeField] SceneVideo openingVideo;
    [SerializeField] DialogueSO openingDialogue;

    bool starting;

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
        if (starting) return;
        starting = true;
        btnStart.interactable = false;
        btnQuit.interactable = false;
        StartCoroutine(PlayOpening());
    }

    IEnumerator PlayOpening()
    {
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = true;
            fadeImage.DOKill();
            yield return fadeImage.DOFade(1f, fadeDuration).WaitForCompletion();
        }

        if (openingVideo != null)
        {
            AudioManager.Instance?.StopBGM();
            openingVideo.Play();
            while (!openingVideo.HasFrame && !openingVideo.IsFinished && !Input.GetKeyDown(KeyCode.Escape))
                yield return null;
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
            DialogueManager.Instance?.StartDialogue(openingDialogue);

        while ((openingVideo != null && !openingVideo.IsFinished) || DialogueManager.IsActive)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                openingVideo?.Skip();
                DialogueManager.Instance?.CancelDialogue();
                break;
            }
            yield return null;
        }

        var load = SceneManager.LoadSceneAsync(SceneLoader.ToName(GameScene.Level0));
        if (load != null) yield return load;
    }

    void QuitGame()
    {
        Application.Quit();
    }
}
