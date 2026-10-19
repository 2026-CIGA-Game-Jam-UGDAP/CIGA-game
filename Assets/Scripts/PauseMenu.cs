using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

/// <summary>
/// ESC 暂停菜单。挂在 MainCanvas 下的 PausePanel 上。
/// GameManager 调 Open()/Close() 控制显隐。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI 引用")]
    public CanvasGroup overlayGroup;
    public RectTransform menuBox;
    public Button btnResume;
    public Button btnRestart;
    public Button btnLobby;
    public Button btnQuit;

    bool isOpen;
    float timeScaleBeforePause = 1f;
    static PauseMenu openMenu;

    public static bool IsOpen => openMenu != null && openMenu.isOpen;
    internal static float ResumeTimeScale => openMenu != null ? openMenu.timeScaleBeforePause : Time.timeScale;

    void Awake()
    {
        btnResume.onClick.AddListener(Close);
        btnRestart.onClick.AddListener(Restart);
        btnLobby.onClick.AddListener(GoToLobby);
        btnQuit.onClick.AddListener(QuitGame);
    }

    void OnEnable()
    {
        // 每次激活时重置为隐藏状态
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        menuBox.localScale = Vector3.zero;
    }

    public void Open()
    {
        if (isOpen) return;
        timeScaleBeforePause = DialogueManager.IsActive ? DialogueManager.Instance.ResumeTimeScale : Time.timeScale;
        isOpen = true;
        openMenu = this;
        overlayGroup.DOKill();
        menuBox.DOKill();
        gameObject.SetActive(true);

        overlayGroup.blocksRaycasts = true;
        overlayGroup.DOFade(1f, 0.25f).SetUpdate(true);

        menuBox.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);

        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!isOpen) return;
        ReleaseTimePause();
        overlayGroup.DOKill();
        menuBox.DOKill();

        overlayGroup.blocksRaycasts = false;
        overlayGroup.DOFade(0f, 0.2f).SetUpdate(true);

        menuBox.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() => gameObject.SetActive(false));
    }

    void ReleaseTimePause()
    {
        if (!isOpen) return;
        isOpen = false;
        if (openMenu != this) return;
        openMenu = null;
        if (!DialogueManager.IsActive)
            Time.timeScale = timeScaleBeforePause;
    }

    void OnDisable()
    {
        ReleaseTimePause();
    }

    void PrepareSceneChange()
    {
        overlayGroup.DOKill();
        menuBox.DOKill();
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.CancelDialogue();
        ReleaseTimePause();
        gameObject.SetActive(false);
    }

    void Restart()
    {
        PrepareSceneChange();
        var gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
            gm.RestartGame();
    }

    void GoToLobby()
    {
        PrepareSceneChange();
        SceneManager.LoadScene("Lobby");
    }

    void QuitGame()
    {
        Application.Quit();
    }
}
