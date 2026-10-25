using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;

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
    [Header("音量设置")]
    public Slider musicSlider;
    public Slider sfxSlider;
    public TMP_Text musicValueText;
    public TMP_Text sfxValueText;

    bool isOpen;
    float timeScaleBeforePause = 1f;
    static PauseMenu openMenu;
    GameObject previousSelection;
    bool cursorWasVisible;
    CursorLockMode previousCursorLock;

    public static bool IsOpen => openMenu != null && openMenu.isOpen;
    internal static float ResumeTimeScale => openMenu != null ? openMenu.timeScaleBeforePause : Time.timeScale;

    void Awake()
    {
        btnResume.onClick.AddListener(Close);
        btnRestart.onClick.AddListener(Restart);
        btnLobby.onClick.AddListener(GoToLobby);
        btnQuit.onClick.AddListener(QuitGame);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(SetSfxVolume);
    }

    void OnEnable()
    {
        // 每次激活时重置为隐藏状态
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;
        menuBox.localScale = Vector3.zero;
    }

    public void Open()
    {
        if (isOpen) return;
        timeScaleBeforePause = DialogueManager.IsActive ? DialogueManager.Instance.ResumeTimeScale : Time.timeScale;
        isOpen = true;
        openMenu = this;
        cursorWasVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        overlayGroup.DOKill();
        menuBox.DOKill();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshVolumeControls();

        if (EventSystem.current != null)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(btnResume.gameObject);
        }
        AudioManager.Instance?.SetGameplayPaused(true);
        DialogueManager.Instance?.SetMenuPaused(true);

        overlayGroup.blocksRaycasts = true;
        overlayGroup.interactable = true;
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
        overlayGroup.interactable = false;
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
        Cursor.visible = cursorWasVisible;
        Cursor.lockState = previousCursorLock;
        AudioManager.Instance?.SetGameplayPaused(false);
        DialogueManager.Instance?.SetMenuPaused(false);
        PlayerPrefs.Save();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        if (!DialogueManager.IsActive)
            Time.timeScale = timeScaleBeforePause;
    }

    void OnDisable()
    {
        overlayGroup.DOKill();
        menuBox.DOKill();
        ReleaseTimePause();
    }

    void RefreshVolumeControls()
    {
        var audio = AudioManager.Instance;
        if (musicSlider != null)
        {
            musicSlider.interactable = audio != null;
            musicSlider.SetValueWithoutNotify(audio != null ? audio.MusicVolume : 0f);
        }
        if (sfxSlider != null)
        {
            sfxSlider.interactable = audio != null;
            sfxSlider.SetValueWithoutNotify(audio != null ? audio.SfxVolume : 0f);
        }
        UpdateVolumeLabels();
    }

    void SetMusicVolume(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
        UpdateVolumeLabels();
    }

    void SetSfxVolume(float value)
    {
        AudioManager.Instance?.SetSfxVolume(value);
        UpdateVolumeLabels();
    }

    void UpdateVolumeLabels()
    {
        if (musicValueText != null && musicSlider != null)
            musicValueText.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        if (sfxValueText != null && sfxSlider != null)
            sfxValueText.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
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
