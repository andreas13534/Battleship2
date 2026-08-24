using UnityEngine;

[DisallowMultipleComponent]
public sealed class NavalBackgroundMusic : MonoBehaviour
{
    public const string MusicResourcePath = "Audio/ColdWaterProtocol";
    private const float BackgroundVolume = 0.26f;
    private static NavalBackgroundMusic instance;
    private static bool playbackAllowed;
    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePlaying()
    {
        if (instance != null) return;

        GameObject musicObject = new GameObject("Naval Background Music");
        DontDestroyOnLoad(musicObject);
        instance = musicObject.AddComponent<NavalBackgroundMusic>();
    }

private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        AudioClip music = Resources.Load<AudioClip>(MusicResourcePath);
        if (music == null)
        {
            Debug.LogWarning("Background music could not be loaded from Resources/" + MusicResourcePath + ".");
            return;
        }

        source = gameObject.AddComponent<AudioSource>();
        source.clip = music;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = BackgroundVolume;

        if (playbackAllowed)
        {
            StartPlayback();
        }
    }

public static void AllowPlayback()
    {
        playbackAllowed = true;
        if (instance != null)
        {
            instance.StartPlayback();
        }
    }

private void StartPlayback()
    {
        if (source == null || source.isPlaying)
        {
            return;
        }

        source.Play();
    }



    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }


[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlaybackGate()
    {
        instance = null;
        playbackAllowed = false;
    }
}
