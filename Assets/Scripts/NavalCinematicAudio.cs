using System.Collections;
using UnityEngine;

public sealed partial class NavalGameController
{
    private enum CinematicSfxChannel
    {
        Explosion,
        JetFlyby
    }

    private const string ExplosionSfxPath = "Audio/SFX/RaptorExplosion";
    private const string JetFlybySfxPath = "Audio/SFX/RaptorFlyby";

    private AudioSource explosionSfxSource;
    private AudioSource jetFlybySfxSource;
    private AudioClip explosionSfx;
    private AudioClip jetFlybySfx;
    private int explosionSfxGeneration;
    private int jetFlybySfxGeneration;

    private void InitializeCinematicAudio()
    {
        explosionSfx = Resources.Load<AudioClip>(ExplosionSfxPath);
        jetFlybySfx = Resources.Load<AudioClip>(JetFlybySfxPath);

        explosionSfxSource = CreateCinematicAudioSource();
        jetFlybySfxSource = CreateCinematicAudioSource();

        WarnIfCinematicClipMissing(explosionSfx, ExplosionSfxPath);
        WarnIfCinematicClipMissing(jetFlybySfx, JetFlybySfxPath);
    }

    private AudioSource CreateCinematicAudioSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }

    private static void WarnIfCinematicClipMissing(AudioClip clip, string resourcePath)
    {
        if (clip == null) Debug.LogWarning("Cinematic SFX fehlt: Resources/" + resourcePath);
    }

    private void PlayExplosionSfx()
    {
        explosionSfxGeneration++;
        PlayTimedCinematicSfx(explosionSfxSource, explosionSfx, 0.3f, 1f, 1.02f, 1.45f,
            CinematicSfxChannel.Explosion, explosionSfxGeneration);
    }

    private void PlayJetFlybySfx()
    {
        jetFlybySfxGeneration++;
        PlayTimedCinematicSfx(jetFlybySfxSource, jetFlybySfx, 0.34f, 1.3f, 0f, 1.25f,
            CinematicSfxChannel.JetFlyby, jetFlybySfxGeneration);
    }

    private void PlayTimedCinematicSfx(
        AudioSource source,
        AudioClip clip,
        float volume,
        float pitch,
        float startTime,
        float duration,
        CinematicSfxChannel channel,
        int generation)
    {
        if (source == null || clip == null) return;

        source.Stop();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
        source.Play();
        StartCoroutine(StopCinematicSfxAfter(source, duration, channel, generation));
    }

    private IEnumerator StopCinematicSfxAfter(
        AudioSource source,
        float duration,
        CinematicSfxChannel channel,
        int generation)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (source != null && generation == CurrentCinematicSfxGeneration(channel)) source.Stop();
    }

    private int CurrentCinematicSfxGeneration(CinematicSfxChannel channel)
    {
        return channel == CinematicSfxChannel.Explosion
            ? explosionSfxGeneration
            : jetFlybySfxGeneration;
    }
}
