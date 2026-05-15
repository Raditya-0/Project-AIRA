using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace AIRA.MiniGames.SpaceShooter
{
public class SoundManipulator : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioMixer m_audioMixer;

    [Header("UI Sliders")]
    [SerializeField] private Slider m_masterSlider;
    [SerializeField] private Slider m_bgmSlider;
    [SerializeField] private Slider m_sfxSlider;

    private void Start()
    {
        // Set default volume atau ambil dari PlayerPrefs jika ada
        if (m_masterSlider != null) m_masterSlider.onValueChanged.AddListener(SetMasterVolume);
        if (m_bgmSlider != null) m_bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        if (m_sfxSlider != null) m_sfxSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    public void SetMasterVolume(float volume)
    {
        // Mengubah nilai linear slider (0 ke 1) menjadi Logarithmic (dB)
        // Nilai -80f adalah sunyi total (muted)
        float dB = Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
        m_audioMixer.SetFloat("MasterVolume", dB);
    }

    public void SetBGMVolume(float volume)
    {
        float dB = Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
        m_audioMixer.SetFloat("BGMVolume", dB);
    }

    public void SetSFXVolume(float volume)
    {
        float dB = Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;
        m_audioMixer.SetFloat("SFXVolume", dB);
    }
}
}