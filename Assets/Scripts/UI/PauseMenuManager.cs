using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using AIRA.Voice;

namespace AIRA.UI
{
public class PauseMenuManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject _pausePanel;

    [Header("Aira Message")]
    [SerializeField] private TMP_Text _airaMessageText;
    [SerializeField] private string[] _airaMessages = new string[]
    {
        "During the break I will always wait for you",
        "Take your time, I'm not going anywhere!",
        "Need a rest? I'll be right here when you're back.",
        "Don't forget to drink water, okay?",
        "I'll keep the conversation warm for you~"
    };

    [Header("Settings Panel")]
    [SerializeField] private GameObject _settingsPanel;

    [Header("Post Processing")]
    [SerializeField] private Volume _globalVolume;
    [SerializeField] private float  _blurStart    = 0f;
    [SerializeField] private float  _blurEnd      = 15f;
    [SerializeField] private float  _blurDuration = 0.3f;

    [Header("Scene")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip   _resumeSound;
    [SerializeField] private AudioClip   _exitSound;

    private bool      _isPaused;
    private Coroutine _blurCoroutine;

    // Sembunyikan panel awal
    private void Awake()
    {
        if (_pausePanel != null)
            _pausePanel.SetActive(false);

        if (_globalVolume != null && _globalVolume.profile.TryGet(out DepthOfField dof))
        {
            dof.active            = false;
            dof.gaussianEnd.value = 0f;
        }
    }

    // Deteksi tombol ESC
    private void Update()
    {
        if (_isPaused && (_pausePanel == null || !_pausePanel.activeSelf))
        {
            _isPaused = false;
            Time.timeScale = 1f;
        }

        var currentState = GameManager.Instance?.CurrentState;
        if (currentState == GameManager.GameState.MINIGAME_PLAYING ||
            currentState == GameManager.GameState.MINIGAME_INTRO  ||
            currentState == GameManager.GameState.MINIGAME_RESULT)
            return;

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (IsMinigameActive()) return;

        if (GameManager.Instance != null && (
            GameManager.Instance.CurrentState == GameManager.GameState.MINIGAME_INTRO ||
            GameManager.Instance.CurrentState == GameManager.GameState.MINIGAME_PLAYING ||
            GameManager.Instance.CurrentState == GameManager.GameState.MINIGAME_RESULT))
            return;

        if (_isPaused)
            ClosePause();
        else
            OpenPause();
    }

    // Cek state minigame aktif
    private bool IsMinigameActive()
    {
        if (GameManager.Instance == null) return false;
        var s = GameManager.Instance.CurrentState;
        return s == GameManager.GameState.MINIGAME_PLAYING
            || s == GameManager.GameState.MINIGAME_INTRO
            || s == GameManager.GameState.MINIGAME_RESULT
            || s == GameManager.GameState.MINIGAME_PLATFORMER
            || s == GameManager.GameState.MINIGAME_SPACESHOOTER;
    }

    // Buka menu pause
    public void OpenPause()
    {
        _isPaused      = true;
        Time.timeScale = 0f;

        TTSManager.Instance?.StopSpeaking();

        if (_airaMessageText != null)
        {
            _airaMessageText.text = (_airaMessages != null && _airaMessages.Length > 0)
                ? _airaMessages[Random.Range(0, _airaMessages.Length)]
                : "";
        }

        if (_pausePanel != null)
            _pausePanel.SetActive(true);

        StartBlur(true);
    }

    // Tutup menu pause
    public void ClosePause()
    {
        _isPaused = false;

        if (_pausePanel != null)
            _pausePanel.SetActive(false);

        StartBlur(false);

        // Jangan restore timeScale kalau tutorial masih aktif
        bool tutorialStillOpen = TutorialPanel.Instance != null
            && TutorialPanel.Instance.IsTutorialOpen;
        if (!tutorialStillOpen)
            Time.timeScale = 1f;
    }

    // Tombol resume ditekan
    public void OnResumeClicked()
    {
        StartCoroutine(PlaySoundThen(_resumeSound, () => ClosePause()));
    }

    // Toggle panel settings
    public void OnSettingsClicked()
    {
        if (_settingsPanel == null) return;
        _settingsPanel.SetActive(!_settingsPanel.activeSelf);
    }

    // Kembali ke main menu
    public void OnMainMenuClicked()
    {
        StartCoroutine(PlaySoundThen(_exitSound, () =>
        {
            Time.timeScale = 1f;
            TTSManager.Instance?.StopSpeaking();
            SceneManager.LoadScene(_mainMenuSceneName);
        }));
    }

    // Mainkan audio lalu jalankan action
    private IEnumerator PlaySoundThen(AudioClip clip, System.Action action)
    {
        if (clip != null && _audioSource != null
            && _audioSource.gameObject.activeInHierarchy
            && _audioSource.enabled)
        {
            _audioSource.PlayOneShot(clip);
            yield return new WaitForSecondsRealtime(clip.length);
        }
        action?.Invoke();
    }

    // Mulai atau hentikan blur
    private void StartBlur(bool blurIn)
    {
        if (_blurCoroutine != null)
            StopCoroutine(_blurCoroutine);

        if (_globalVolume == null) return;

        _blurCoroutine = StartCoroutine(blurIn ? BlurIn() : BlurOut());
    }

    // Lerp blur masuk
    private IEnumerator BlurIn()
    {
        if (!_globalVolume.profile.TryGet(out DepthOfField dof)) yield break;

        dof.active = true;
        float elapsed = 0f;
        while (elapsed < _blurDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _blurDuration);
            dof.gaussianStart.value = Mathf.Lerp(0f, _blurStart, t);
            dof.gaussianEnd.value   = Mathf.Lerp(0f, _blurEnd, t);
            yield return null;
        }

        dof.gaussianStart.value = _blurStart;
        dof.gaussianEnd.value   = _blurEnd;
        _blurCoroutine          = null;
    }

    // Lerp blur keluar
    private IEnumerator BlurOut()
    {
        if (!_globalVolume.profile.TryGet(out DepthOfField dof)) yield break;

        float elapsed = 0f;
        while (elapsed < _blurDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _blurDuration);
            dof.gaussianEnd.value = Mathf.Lerp(_blurEnd, 0f, t);
            yield return null;
        }

        dof.gaussianStart.value = 0f;
        dof.gaussianEnd.value   = 0f;
        dof.active              = false;
        _blurCoroutine          = null;
    }
}
}
