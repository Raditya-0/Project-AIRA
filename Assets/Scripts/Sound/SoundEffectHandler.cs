using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter{

public class SoundEffectHandler : MonoBehaviour
{
    [SerializeField] private List<AudioClip> m_generators;
    [SerializeField] private Vector2 m_minMaxPitch;
    [SerializeField] private List<AudioSource> m_audioSources;

    [Header("Fade Settings")]
    [SerializeField] private float m_fadeDuration = 0.5f;
    [SerializeField] private float m_volume       = 1f;
    private Coroutine m_fadeRoutine;

    public void Play()
    {
        if (m_generators == null || m_generators.Count == 0) return;
        if (m_audioSources == null || m_audioSources.Count == 0) return;

        int clipIndex = Random.Range(0, m_generators.Count);
        AudioClip selectedClip = m_generators[clipIndex];

        int sourceIndex = Random.Range(0, m_audioSources.Count);
        AudioSource selectedSource = m_audioSources[sourceIndex];

        if (selectedSource != null)
        {
            selectedSource.clip = selectedClip;
            selectedSource.pitch = Random.Range(m_minMaxPitch.x, m_minMaxPitch.y);

            // Mulai dari volume 0 agar Fade In terasa halus
            selectedSource.volume = 0f;
            selectedSource.Play();

            FadeIn(selectedSource);
        }
    }

    public void FadeIn(AudioSource source)
    {
        if (m_fadeRoutine != null) StopCoroutine(m_fadeRoutine);
        m_fadeRoutine = StartCoroutine(FadeAudioSource(source, m_volume));
    }

    public void FadeOut(AudioSource source)
    {
        if (m_fadeRoutine != null) StopCoroutine(m_fadeRoutine);
        m_fadeRoutine = StartCoroutine(FadeAudioSource(source, 0f));
    }

    private IEnumerator FadeAudioSource(AudioSource source, float targetVolume)
    {
        float startVolume = source.volume;
        float time = 0;

        while (time < m_fadeDuration)
        {
            if (source == null) yield break;

            // Gunakan unscaledDeltaTime agar suara tetap fade-in saat game pause
            time += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, time / m_fadeDuration);
            yield return null;
        }
        source.volume = targetVolume;
    }

    public void StopWithFade()
    {
        foreach (var source in m_audioSources)
        {
            if (source.isPlaying)
            {
                FadeOut(source);
            }
        }
    }
}
}