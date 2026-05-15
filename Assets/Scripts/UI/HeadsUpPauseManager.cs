using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using TMPro;
using AIRA.Voice;

namespace AIRA.UI
{
    public class HeadsUpPauseManager : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _pausePanel;

        [Header("Score")]
        [SerializeField] private TMP_Text _scoreValueText;

        [Header("Aira Message")]
        [SerializeField] private TMP_Text _airaMessageText;
        [SerializeField] private string[] _airaMessages = new string[]
        {
            "I'm waiting for you to give me another clue",
            "Take a breather! You're doing great.",
            "Don't worry, I'll keep the score safe for you~",
            "Ready to jump back in? I believe in you!",
            "A short break never hurt anyone. Let's go again soon!"
        };

        [Header("Settings Panel")]
        [SerializeField] private GameObject _settingsPanel;

        [Header("Post Processing")]
        [SerializeField] private Volume _globalVolume;
        [SerializeField] private float  _blurEnd      = 15f;
        [SerializeField] private float  _blurDuration = 0.3f;

        [Header("Scene")]
        [SerializeField] private string _mainSceneName = "MainScene";

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip   _resumeSound;
        [SerializeField] private AudioClip   _restartSound;
        [SerializeField] private AudioClip   _exitSound;

        private bool      _isPaused;
        private Coroutine _blurCoroutine;

        // Sembunyikan panel awal
        private void Awake()
        {
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
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

            if (GameManager.Instance == null) return;
            var state = GameManager.Instance.CurrentState;
            if (state != GameManager.GameState.MINIGAME_INTRO &&
                state != GameManager.GameState.MINIGAME_PLAYING &&
                state != GameManager.GameState.MINIGAME_RESULT)
                return;

            if (_isPaused)
                ClosePause();
            else
                OpenPause();
        }

        // Buka menu pause
        public void OpenPause()
        {
            _isPaused      = true;
            Time.timeScale = 0f;
            TTSManager.Instance?.StopSpeaking();

            // TODO: Expose property Score publik di HeadsUpGame
            if (_scoreValueText != null)
                _scoreValueText.text = HeadsUpGame.Instance != null ? "—" : "—";

            if (_airaMessageText != null)
            {
                _airaMessageText.text = (_airaMessages != null && _airaMessages.Length > 0)
                    ? _airaMessages[Random.Range(0, _airaMessages.Length)]
                    : "";
            }

            _pausePanel.SetActive(true);
            StartBlur(true);
        }

        // Tutup menu pause
        public void ClosePause()
        {
            _isPaused = false;
            StartBlur(false);
            _pausePanel.SetActive(false);
            Time.timeScale = 1f;
        }

        // Tombol resume ditekan
        public void OnResumeClicked()
        {
            StartCoroutine(PlaySoundThen(_resumeSound, () => ClosePause()));
        }

        // Restart game dari awal
        public void OnRestartClicked()
        {
            StartCoroutine(PlaySoundThen(_restartSound, () =>
            {
                ClosePause();
                HeadsUpGame.Instance?.StartGame();
            }));
        }

        // Toggle panel settings
        public void OnSettingsClicked()
        {
            if (_settingsPanel == null) return;
            _settingsPanel.SetActive(!_settingsPanel.activeSelf);
        }

        // Kembali ke Aira Room
        public void OnAiraRoomClicked()
        {
            StopAllCoroutines();
            _isPaused = false;
            _pausePanel?.SetActive(false);
            Time.timeScale = 1f;
            TTSManager.Instance?.StopSpeaking();
            HeadsUpGame.Instance?.EndGame();
            GameManager.Instance?.ChangeState(GameManager.GameState.IDLE);
            Destroy(gameObject);
            SceneManager.LoadScene(_mainSceneName);
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
                elapsed              += Time.unscaledDeltaTime;
                float t               = Mathf.Clamp01(elapsed / _blurDuration);
                dof.gaussianEnd.value = Mathf.Lerp(0f, _blurEnd, t);
                yield return null;
            }

            dof.gaussianEnd.value = _blurEnd;
            _blurCoroutine        = null;
        }

        // Lerp blur keluar
        private IEnumerator BlurOut()
        {
            if (!_globalVolume.profile.TryGet(out DepthOfField dof)) yield break;

            float elapsed = 0f;
            while (elapsed < _blurDuration)
            {
                elapsed              += Time.unscaledDeltaTime;
                float t               = Mathf.Clamp01(elapsed / _blurDuration);
                dof.gaussianEnd.value = Mathf.Lerp(_blurEnd, 0f, t);
                yield return null;
            }

            dof.gaussianEnd.value = 0f;
            dof.active            = false;
            _blurCoroutine        = null;
        }
    }
}
