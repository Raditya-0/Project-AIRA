using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;
using Whisper;
using Whisper.Utils;
using AIRA.Core;
using AIRA.UI;

namespace AIRA.Voice
{
    public class STTManager : MonoBehaviour, ISTTProvider
    {
        public static STTManager Instance { get; private set; }

        // Event state listening berubah
        public static event Action<bool> OnListeningStateChanged;

        // Event hasil STT final
        public event Action<string> OnResultReady;

        [Header("Referensi")]
        [SerializeField] private TMP_InputField _inputField;

        [Header("Whisper Config")]
        [SerializeField] private WhisperManager _whisperManager;
        [SerializeField] private MicrophoneRecord _micRecord;
        [SerializeField] private string _language = "en";

        [Header("Microphone Device")]
        [SerializeField] private int _deviceIndex = 0;

        [Header("Settings")]
        [SerializeField] private bool _autoSend = true;

        [Header("STT Filter Settings")]
        [SerializeField] private float _minTokenConfidence = 0.4f;

        private static readonly string[] k_HallucinationBlacklist = {
            "[BLANK_AUDIO]", "[ Silence ]", "[ silence ]", "(silence)",
            "[MUSIC]", "[music]", "♪", "Thank you.", "Thanks for watching.",
            "Thank you for watching.", "Please subscribe.", "[ Pause ]"
        };

        // Flag inisialisasi selesai
        public bool IsInitialized { get; private set; }

        private bool      _isListening;
        private bool      _isMicOn;
        private bool      _isPaused;
        private bool      _shouldTranscribe;
        private Color     _normalColor;
        private Coroutine _ttsSubscribeCoroutine;

        // Awake setup singleton
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Start inisialisasi Whisper
        private void Start()
        {
            if (_inputField != null)
                _normalColor = _inputField.textComponent.color;

            _whisperManager.language     = _language;
            _whisperManager.enableTokens = true;
            _micRecord.echo              = false;

            // Terapkan device awal
            ApplyDeviceToMicRecord();
            if (Debug.isDebugBuild)
            {
                var devices = GetAvailableDevices();
                for (int i = 0; i < devices.Length; i++)
                    Debug.Log($"[STTManager] Device [{i}]: {devices[i]}");
            }

            StartCoroutine(WaitForWhisperReady());
            _ttsSubscribeCoroutine = StartCoroutine(SubscribeToTTSEvents());
        }

        // Subscribe event game dan mic
        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
            _micRecord.OnRecordStop    += HandleRecordStop;
        }

        // Lepas event game dan mic
        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
            _micRecord.OnRecordStop    -= HandleRecordStop;

            if (_ttsSubscribeCoroutine != null)
            {
                StopCoroutine(_ttsSubscribeCoroutine);
                _ttsSubscribeCoroutine = null;
            }
            if (TTSManager.Instance != null)
            {
                TTSManager.Instance.OnSpeakStart -= OnTTSSpeakStart;
                TTSManager.Instance.OnSpeakEnd   -= OnTTSSpeakEnd;
            }
        }

        // Pause/resume sesuai state
        private void HandleStateChanged(
            GameManager.GameState prev,
            GameManager.GameState next)
        {
            switch (next)
            {
                case GameManager.GameState.THINKING:
                case GameManager.GameState.SPEAKING:
                    PauseListening();
                    break;
                case GameManager.GameState.IDLE:
                    if (_isListening)
                        ResumeListening();
                    else
                        StartListening();
                    break;
                case GameManager.GameState.MINIGAME_PLATFORMER:
                case GameManager.GameState.MINIGAME_SPACESHOOTER:
                    if (!_isListening)
                        StartListening();
                    else
                        ResumeListening();
                    break;
            }
        }

        // Cek setting STT aktif
        private bool IsSTTEnabled()
            => AIRASettings.Instance == null || AIRASettings.Instance.STTEnabled;

        // Mulai dengarkan mic
        public void StartListening()
        {
            if (_isListening || !IsSTTEnabled()) return;

            _isListening      = true;
            _isMicOn          = true;
            _isPaused         = false;
            _shouldTranscribe = true;
            OnListeningStateChanged?.Invoke(true);

            _micRecord.StartRecord();
        }

        // Berhenti dengarkan mic
        public void StopListening()
        {
            if (!_isListening) return;

            _isListening      = false;
            _isMicOn          = false;
            _isPaused         = false;
            _shouldTranscribe = false;

            _micRecord.StopRecord();
            OnListeningStateChanged?.Invoke(false);
        }

        // Pause sementara saat AI bicara
        public void PauseListening()
        {
            if (!_isListening || _isPaused) return;

            _isPaused         = true;
            _shouldTranscribe = false;

            _micRecord.StopRecord();
            OnListeningStateChanged?.Invoke(false);
        }

        // Resume setelah AI selesai
        public void ResumeListening()
        {
            if (!_isListening || !_isPaused || !IsSTTEnabled()) return;

            _isPaused         = false;
            _shouldTranscribe = true;
            OnListeningStateChanged?.Invoke(true);

            _micRecord.StartRecord();
        }

        // Toggle mic button on/off
        public void ToggleListening()
        {
            if (_isListening)
                StopListening();
            else
                StartListening();
        }

        // Ambil semua device mic
        public string[] GetAvailableDevices()
            => Microphone.devices;

        // Set device berdasarkan index
        public void SetDevice(int index)
        {
            if (index < 0 || index >= Microphone.devices.Length) return;
            _deviceIndex = index;

            if (_isListening && !_isPaused)
            {
                _micRecord.StopRecord();
                ApplyDeviceToMicRecord();
                _micRecord.StartRecord();
            }
            else
            {
                ApplyDeviceToMicRecord();
            }
        }

        // Isi dropdown dengan devices
        public void PopulateDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            var options = new List<string>();
            foreach (var device in Microphone.devices)
                options.Add(device);
            dropdown.AddOptions(options);
            dropdown.onValueChanged.AddListener(SetDevice);
        }

        // Terapkan device ke MicrophoneRecord
        private void ApplyDeviceToMicRecord()
        {
            string deviceName = Microphone.devices.Length > _deviceIndex
                ? Microphone.devices[_deviceIndex]
                : null;
            _micRecord.SelectedMicDevice = deviceName;
            Debug.Log($"[STTManager] Mic device: {deviceName ?? "Default"}");
        }

        // Handle rekaman selesai, transcribe ke Whisper
        private async void HandleRecordStop(AudioChunk chunk)
        {
            if (!_shouldTranscribe || chunk.Data == null) return;

            var result = await _whisperManager.GetTextAsync(chunk.Data, chunk.Frequency, chunk.Channels);
            if (result == null) return;

            // Layer 1: cek bahasa
            if (!string.IsNullOrEmpty(result.Language) && result.Language != "en")
            {
                TriggerAiraReaction(ReactionType.WrongLanguage);
                RestartIfActive();
                return;
            }

            // Layer 2: strip bracket tags lalu cek sisa teks
            string raw      = result.Result ?? "";
            string stripped = Regex.Replace(raw, @"^\s*\[[^\]]*\]\s*", "").Trim();
            if (string.IsNullOrWhiteSpace(stripped))
            {
                RestartIfActive();
                return;
            }

            // Layer 3: token confidence
            if (result.Segments != null && result.Segments.Count > 0)
            {
                float totalProb = 0f;
                int tokenCount  = 0;
                foreach (var seg in result.Segments)
                {
                    if (seg.Tokens == null) continue;
                    foreach (var token in seg.Tokens)
                    {
                        if (token.IsSpecial) continue;
                        totalProb += token.Prob;
                        tokenCount++;
                    }
                }
                if (tokenCount > 0 && (totalProb / tokenCount) < _minTokenConfidence)
                {
                    TriggerAiraReaction(ReactionType.LowConfidence);
                    RestartIfActive();
                    return;
                }
            }

            // Layer 4: minimal satu kata bermakna
            string cleaned  = TextUtils.CleanSTTResult(stripped);
            int    wordCount = cleaned.Split(
                new char[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries).Length;
            if (string.IsNullOrWhiteSpace(cleaned) || wordCount < 1)
            {
                RestartIfActive();
                return;
            }

            // Lolos semua filter — proses normal
            if (_inputField != null) _inputField.text = cleaned;
            OnResultReady?.Invoke(cleaned);
            if (_autoSend) SendToChat(cleaned);
            RestartIfActive();
        }

        private enum ReactionType { DidntHear, LowConfidence, WrongLanguage }

        private static readonly string[] k_DidntHearPool = {
            "Hmm? Did you say something?",
            "I didn't catch that, could you say it again?",
            "Sorry, I think I missed that!"
        };
        private static readonly string[] k_LowConfidencePool = {
            "Huh, I didn't quite catch that, could you speak a bit clearer?",
            "I heard something but I'm not sure what — say that again?",
            "Could you repeat that? It sounded a bit unclear."
        };
        private static readonly string[] k_WrongLanguagePool = {
            "Hee, I can't understand that language! Speak English to me~",
            "I only understand English, sorry about that!",
            "Hmm, that didn't sound like English to me!"
        };

        // Trigger reaksi Aira sesuai kondisi
        private void TriggerAiraReaction(ReactionType type)
        {
            string[] pool = type switch {
                ReactionType.WrongLanguage  => k_WrongLanguagePool,
                ReactionType.LowConfidence  => k_LowConfidencePool,
                _                           => k_DidntHearPool
            };
            string msg = pool[UnityEngine.Random.Range(0, pool.Length)];
            GameManager.Instance?.ProcessAiraReaction(msg);
        }

        // Restart rekaman jika masih aktif
        private void RestartIfActive()
        {
            if (_isListening && !_isPaused)
            {
                _shouldTranscribe = true;
                _micRecord.StartRecord();
            }
        }

        // Tunggu Whisper model loaded
        private IEnumerator WaitForWhisperReady()
        {
            yield return new WaitUntil(() =>
                _whisperManager != null && _whisperManager.IsLoaded);
            ApplyDeviceToMicRecord();
            IsInitialized = true;
            LoadingGate.Instance?.SetSTTReady();
            Debug.Log("[STTManager] Whisper siap.");
        }

        // Tunggu TTS siap, subscribe event
        private IEnumerator SubscribeToTTSEvents()
        {
            yield return new WaitUntil(() => TTSManager.Instance != null);
            TTSManager.Instance.OnSpeakStart += OnTTSSpeakStart;
            TTSManager.Instance.OnSpeakEnd   += OnTTSSpeakEnd;
            _ttsSubscribeCoroutine = null;
        }

        // Pause mic saat TTS mulai
        private void OnTTSSpeakStart()
        {
            if (_isListening && !_isPaused)
                PauseListening();
        }

        // Restart mic setelah TTS selesai
        private void OnTTSSpeakEnd()
        {
            if (!_isMicOn) return;
            StartCoroutine(RestartListening());
        }

        // Stop lalu start ulang mic
        private IEnumerator RestartListening()
        {
            StopListening();
            _isMicOn = true;
            yield return new WaitForSeconds(0.2f);
            StartListening();
        }

        // Kirim teks ke chat
        private void SendToChat(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (ChatUIManager.Instance != null)
            {
                if (_inputField != null)
                    _inputField.text = text;
                ChatUIManager.Instance.OnUserSubmit();
                return;
            }

            GameManager.Instance?.ProcessUserInput(text);
        }

        // Cleanup saat komponen destroy
        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
