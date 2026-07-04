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
    public Button btnLobby;
    public Button btnQuit;

    bool isOpen;

    void Awake()
    {
        btnResume.onClick.AddListener(Close);
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
        isOpen = true;
        gameObject.SetActive(true);

        overlayGroup.blocksRaycasts = true;
        overlayGroup.DOFade(1f, 0.25f).SetUpdate(true);

        menuBox.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);

        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        overlayGroup.blocksRaycasts = false;
        overlayGroup.DOFade(0f, 0.2f).SetUpdate(true);

        menuBox.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() => gameObject.SetActive(false));

        Time.timeScale = 1f;
    }

    void GoToLobby()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Lobby");
    }

    void QuitGame()
    {
        Application.Quit();
    }
}
