using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Whisper;
using Whisper.Utils;
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

        private bool  _isListening;
        private bool  _isPaused;
        private bool  _shouldTranscribe;
        private Color _normalColor;

        // Awake setup singleton
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Start inisialisasi Whisper
        private void Start()
        {
            if (_inputField != null)
                _normalColor = _inputField.textComponent.color;

            _whisperManager.language = _language;

            // Terapkan device awal
            ApplyDeviceToMicRecord();
            if (Debug.isDebugBuild)
            {
                var devices = GetAvailableDevices();
                for (int i = 0; i < devices.Length; i++)
                    Debug.Log($"[STTManager] Device [{i}]: {devices[i]}");
            }

            LoadingGate.Instance?.SetSTTReady();
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
                    ResumeListening();
                    break;
                case GameManager.GameState.MINIGAME_PLATFORMER:
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

            _isPaused = false;
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
            if (result == null || string.IsNullOrWhiteSpace(result.Result)) return;
            if (result.Result.Contains("[BLANK_AUDIO]")) return;

            string cleaned = TextUtils.CleanSTTResult(result.Result);
            if (string.IsNullOrWhiteSpace(cleaned)) return;

            if (_inputField != null)
                _inputField.text = cleaned;

            OnResultReady?.Invoke(cleaned);

            if (_autoSend)
                SendToChat(cleaned);

            // Restart rekaman jika masih aktif
            if (_isListening && !_isPaused)
            {
                _shouldTranscribe = true;
                _micRecord.StartRecord();
            }
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
