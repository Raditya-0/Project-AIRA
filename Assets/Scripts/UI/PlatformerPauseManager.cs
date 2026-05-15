using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using AIRA.Voice;
using AIRA.MiniGames.Platformer;

namespace AIRA.UI
{
    public class PlatformerPauseManager : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _pausePanel;

        [Header("Level Display")]
        [SerializeField] private TMP_Text _levelValueText;

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
            if (GameManager.Instance.CurrentState != GameManager.GameState.MINIGAME_PLATFORMER) return;

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

            int level = GetCurrentLevel();

            if (_levelValueText != null)
                _levelValueText.text = level.ToString();
            if (_airaMessageText != null)
                _airaMessageText.text = GetContextualMessage(level);

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

        // Restart level in-place
        public void OnRestartClicked()
        {
            StartCoroutine(PlaySoundThen(_restartSound, () =>
            {
                ClosePause();
                PlatformerGame.Instance?.StartGame();
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
                GameManager.Instance?.EndPlatformer();
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

        // Ambil nomor level dari scene
        private int GetCurrentLevel()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Level0"))
            {
                string numStr = sceneName.Substring(sceneName.Length - 2);
                if (int.TryParse(numStr, out int num))
                    return num;
            }
            return 1;
        }

        // Pilih pesan kontekstual per level
        private string GetContextualMessage(int level)
        {
            return level switch
            {
                1 => "We're almost at the checkpoint! Keep your spirits up!",
                2 => "Final level! Let's finish this together!",
                _ => "Take a breather. I'll be right here!"
            };
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
