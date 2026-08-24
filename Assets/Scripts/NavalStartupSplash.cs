using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public sealed partial class NavalGameController
{
    private VisualElement startupSplash;
    private VisualElement startupSplashLogo;

    private void CacheStartupSplashUi(VisualElement root)
    {
        startupSplash = root.Q<VisualElement>("StartupSplash");
        startupSplashLogo = root.Q<VisualElement>("StartupSplashLogo");
    }

    private void StartStartupSplash()
    {
        if (startupSplash == null || startupSplashLogo == null)
        {
            NavalBackgroundMusic.AllowPlayback();
            return;
        }

        GameObject runnerObject = new GameObject("Naval Startup Splash Runner");
        NavalStartupSplashRunner runner = runnerObject.AddComponent<NavalStartupSplashRunner>();
        runner.Begin(startupSplash, startupSplashLogo);
    }
}

[DisallowMultipleComponent]
internal sealed class NavalStartupSplashRunner : MonoBehaviour
{
    private const string AudioResourcePath = "Audio/Splash/Battleship2SplashSting";
    private const float FallbackDuration = 2.65f;
    private const float FadeDuration = 0.34f;

    private VisualElement splash;
    private VisualElement logo;
    private AudioSource audioSource;

    public void Begin(VisualElement splashElement, VisualElement logoElement)
    {
        splash = splashElement;
        logo = logoElement;
        StartCoroutine(PlaySplash());
    }

    private IEnumerator PlaySplash()
    {
        splash.RemoveFromClassList("startup-splash-out");
        logo.RemoveFromClassList("startup-splash-logo-visible");
        splash.style.display = DisplayStyle.Flex;
        splash.BringToFront();

        yield return null;
        yield return new WaitForSecondsRealtime(0.12f);

        AudioClip sting = Resources.Load<AudioClip>(AudioResourcePath);
        if (sting != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = sting;
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.86f;
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("Startup splash audio could not be loaded from Resources/" + AudioResourcePath + ".");
        }

        logo.AddToClassList("startup-splash-logo-visible");

        float holdDuration = sting != null
            ? Mathf.Max(1.9f, sting.length - 0.08f)
            : FallbackDuration;
        yield return new WaitForSecondsRealtime(holdDuration);

        splash.AddToClassList("startup-splash-out");
        yield return new WaitForSecondsRealtime(FadeDuration);

        splash.style.display = DisplayStyle.None;
        NavalBackgroundMusic.AllowPlayback();
        Destroy(gameObject);
    }
}
