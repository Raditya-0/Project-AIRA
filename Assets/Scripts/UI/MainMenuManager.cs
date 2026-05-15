using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AIRA.AI;
using AIRA.Character;
using AIRA.Voice;

namespace AIRA.UI
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _creditsPanel;
        [SerializeField] private GameObject _settingsPanel;

        [Header("Greeting")]
        [SerializeField] private string _defaultGreeting   = "Hi! I'm Aira. Nice to meet you!";
        [SerializeField] private string _returningGreeting = "Welcome back! It's good to see you again.";
        [SerializeField] private float  _greetingDelay     = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip   _closeSound;

        private void Awake()
        {
            if (_creditsPanel != null) _creditsPanel.SetActive(false);
        }

        private void Start()
        {
            StartCoroutine(GreetingSequence());
        }

        private IEnumerator GreetingSequence()
        {
            yield return new WaitUntil(() =>
                LoadingGate.Instance == null || LoadingGate.Instance.IsReady);

            yield return new WaitForSeconds(_greetingDelay);

            TriggerGreeting();
        }

        private void TriggerGreeting()
        {
            if (TTSManager.Instance == null) return;

            bool hasHistory = !string.IsNullOrEmpty(MemoryManager.Instance?.sessionSummary);
            string text     = hasHistory ? _returningGreeting : _defaultGreeting;

            AiraController.Instance?.SetExpression("[HAPPY]");
            TTSManager.Instance.Speak(text, "HAPPY");
        }

        public void OnClickPlay()
        {
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (TTSManager.Instance != null) TTSManager.Instance.StopSpeaking();
            SceneManager.LoadScene("MainScene");
            yield break;
        }

        public void OnClickSettings()
        {
            StartCoroutine(SettingsRoutine());
        }

        private IEnumerator SettingsRoutine()
        {
            if (_settingsPanel != null) _settingsPanel.SetActive(!_settingsPanel.activeSelf);
            yield break;
        }

        public void OnClickCredits()
        {
            StartCoroutine(CreditsRoutine());
        }

        private IEnumerator CreditsRoutine()
        {
            if (_creditsPanel != null) _creditsPanel.SetActive(true);
            yield break;
        }

        // Mulai coroutine tutup credits
        public void OnClickCloseCredits()
        {
            StartCoroutine(CloseWithSound());
        }

        // Tunggu audio selesai tutup
        private IEnumerator CloseWithSound()
        {
            if (_audioSource != null && _audioSource.gameObject.activeInHierarchy && _audioSource.enabled)
            {
                _audioSource.PlayOneShot(_closeSound);
                yield return new WaitUntil(() => !_audioSource.isPlaying);
            }
            if (_creditsPanel != null) _creditsPanel.SetActive(false);
        }

        public void OnClickExit()
        {
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            yield break;
        }
    }
}