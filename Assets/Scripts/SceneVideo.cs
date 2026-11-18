using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>场景里的全屏视频遮罩。播放结束后保留画面，直到切换场景。</summary>
public class SceneVideo : MonoBehaviour
{
    [SerializeField] VideoPlayer player;
    [SerializeField] RawImage screen;
    [SerializeField] AspectRatioFitter aspect;

    const float StartupTimeout = 5f;
    const float PlaybackGrace = 3f;

    float deadline;
    public bool HasFrame { get; private set; }
    public bool IsFinished { get; private set; }

    public void Play()
    {
        gameObject.SetActive(true);
        if (player.clip == null)
        {
            IsFinished = true;
            return;
        }

        aspect.aspectRatio = (float)player.clip.width / player.clip.height;
        player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        player.controlledAudioTrackCount = player.clip.audioTrackCount;
        if (player.controlledAudioTrackCount > 0)
            player.EnableAudioTrack(0, true);
        player.errorReceived += OnError;
        player.loopPointReached += OnEnd;
        player.prepareCompleted += OnPrepared;
        deadline = Time.realtimeSinceStartup + StartupTimeout;
        player.Prepare();
    }

    void OnPrepared(VideoPlayer source)
    {
        if (IsFinished) return;
        if (source.controlledAudioTrackCount > 0 && source.canSetDirectAudioVolume && AudioManager.Instance != null)
            source.SetDirectAudioVolume(0, AudioManager.Instance.MusicVolume);
        source.Play();
    }

    void Update()
    {
        if (IsFinished) return;
        if (!HasFrame && player.frame >= 0 && player.texture != null)
        {
            HasFrame = true;
            screen.texture = player.texture;
            screen.enabled = true;
            deadline = Time.realtimeSinceStartup + (float)player.length + PlaybackGrace;
        }

        if (Time.realtimeSinceStartup > deadline)
            OnError(player, "视频播放超时。");
    }

    public void Skip()
    {
        IsFinished = true;
        player.Pause();
    }

    void OnEnd(VideoPlayer source)
    {
        IsFinished = true;
    }

    void OnError(VideoPlayer source, string message)
    {
        if (IsFinished) return;
        Debug.LogWarning($"[SceneVideo] {source.clip?.name}: {message}", this);
        Skip();
    }

    void OnDisable()
    {
        player.errorReceived -= OnError;
        player.loopPointReached -= OnEnd;
        player.prepareCompleted -= OnPrepared;
        player.Stop();
        screen.texture = null;
    }
}
