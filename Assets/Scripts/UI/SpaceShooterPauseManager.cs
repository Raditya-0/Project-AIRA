using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using AIRA.Voice;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.UI
{
    public class SpaceShooterPauseManager : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _pausePanel;

        [Header("Stats Display")]
        [SerializeField] private TMP_Text _scoreValueText;
        [SerializeField] private TMP_Text _livesValueText;

        [Header("Aira Message")]
        [SerializeField] private TMP_Text _airaMessageText;

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
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.MINIGAME_SPACESHOOTER) return;

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

            bool hasScore = ScoreManager.Instance != null;
            int score     = hasScore ? ScoreManager.Instance.GetCurrentScore() : 0;
            int lives     = hasScore ? ScoreManager.Instance.GetCurrentLives() : 0;

            if (_scoreValueText != null)
                _scoreValueText.text = hasScore ? score.ToString() : "—";
            if (_livesValueText != null)
                _livesValueText.text = hasScore ? $"{lives}" : "—";
            if (_airaMessageText != null)
                _airaMessageText.text = GetContextualMessage(score, lives);

            if (_pausePanel != null)
                _pausePanel.SetActive(true);

            StartBlur(true);
        }

        // Tutup menu pause
        public void ClosePause()
        {
            _isPaused = false;
            StartBlur(false);
            if (_pausePanel != null)
                _pausePanel.SetActive(false);
            Time.timeScale = 1f;
        }

        // Tombol resume ditekan
        public void OnResumeClicked()
        {
            StartCoroutine(PlaySoundThen(_resumeSound, () => ClosePause()));
        }

        // Restart game via soft-reset
        public void OnRestartClicked()
        {
            StartCoroutine(PlaySoundThen(_restartSound, () =>
            {
                ClosePause();
                GameEvents.Instance?.OnRetry();
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
            StartCoroutine(PlaySoundThen(_exitSound, () =>
            {
                GameManager.Instance?.EndSpaceShooter();
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

        // Pilih pesan kontekstual pause
        private string GetContextualMessage(int score, int lives)
        {
            if (lives == 1)
                return "Careful! Only one life left. Stay focused!";
            if (score == 0)
                return "Just getting started! You've got this.";
            if (score >= 500)
                return $"Wow, {score} points already! You're on fire!";
            if (score >= 200)
                return $"Come on, the score is almost {RoundUpToNearest100(score)}. Keep up the good work!";
            return "Take a breather. I'll be here when you're ready!";
        }

        // Bulatkan ke ratusan terdekat
        private int RoundUpToNearest100(int score)
        {
            return ((score / 100) + 1) * 100;
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
