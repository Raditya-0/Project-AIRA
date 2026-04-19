using System;
using System.Collections;
using System.IO;
using UnityEngine;
using TMPro;
using Vosk;
using AIRA.UI;

namespace AIRA.Voice
{
    public class STTManager : MonoBehaviour, ISTTProvider
    {
        public static STTManager Instance { get; private set; }

        // Event state listening berubah
        public static event Action<bool> OnListeningStateChanged;

        // Event hasil STT final (ISTTProvider)
        public event Action<string> OnResultReady;

        [Header("Referensi")]
        [SerializeField] private TMP_InputField _inputField;

        [Header("Vosk Config")]
        [SerializeField] private string _modelPath = "vosk-model-en-us-0.22-lgraph";

        [Header("Settings")]
        [SerializeField] private float _silenceTimeout  = 1.5f;
        [SerializeField] private bool  _autoSend        = true;
        [SerializeField] private Color _hypothesisColor = new Color(1f, 1f, 1f, 0.6f);

        private VoskRecognizer _recognizer;
        private AudioClip      _micClip;
        private bool           _isListening;
        private bool           _isPaused;
        private Coroutine      _silenceCoroutine;
        private Coroutine      _micStreamCoroutine;
        private string         _lastHypothesis = "";
        private Color          _normalColor;

        // Helper parse JSON Vosk result
        [Serializable] private class VoskResult  { public string text    = ""; }
        [Serializable] private class VoskPartial { public string partial = ""; }

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

        // Start inisialisasi semua
        private void Start()
        {
            if (_inputField != null)
                _normalColor = _inputField.textComponent.color;

            InitVosk();
        }

        // Subscribe event state game
        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
        }

        // Lepas event state game
        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
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
            }
        }

        // Init model Vosk
        private void InitVosk()
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, _modelPath);

            if (!Directory.Exists(fullPath))
            {
                Debug.LogWarning($"[STTManager] Model tidak ditemukan: {fullPath}");
                return;
            }

            try
            {
                var model = new Model(fullPath);
                _recognizer = new VoskRecognizer(model, 16000f);
                _recognizer.SetMaxAlternatives(0);
                _recognizer.SetWords(true);
                Debug.Log("[STTManager] Vosk berhasil diinisialisasi.");
                // STT siap dipakai
                LoadingGate.Instance?.SetSTTReady();
            }
            catch (Exception e)
            {
                Debug.LogError($"[STTManager] Gagal init Vosk: {e.Message}");
            }
        }

        // Mulai dengarkan mic
        public void StartListening()
        {
            if (_recognizer == null)
            {
                Debug.LogWarning("[STTManager] Recognizer belum siap.");
                return;
            }

            if (_isListening) return;

            _isListening = true;
            _isPaused    = false;
            OnListeningStateChanged?.Invoke(true);

            _micStreamCoroutine = StartCoroutine(MicStreamCoroutine());
        }

        // Berhenti dengarkan mic
        public void StopListening()
        {
            if (!_isListening) return;

            _isListening = false;
            _isPaused    = false;

            if (_silenceCoroutine != null)
            {
                StopCoroutine(_silenceCoroutine);
                _silenceCoroutine = null;
            }

            if (_micStreamCoroutine != null)
            {
                StopCoroutine(_micStreamCoroutine);
                _micStreamCoroutine = null;
            }

            OnListeningStateChanged?.Invoke(false);
        }

        // Pause sementara saat AI bicara
        public void PauseListening()
        {
            if (!_isListening || _isPaused) return;
            _isPaused = true;
            OnListeningStateChanged?.Invoke(false);
        }

        // Resume setelah AI selesai
        public void ResumeListening()
        {
            if (!_isListening || !_isPaused) return;
            _isPaused = false;
            OnListeningStateChanged?.Invoke(true);
        }

        // Toggle mic button on/off
        public void ToggleListening()
        {
            if (_isListening)
                StopListening();
            else
                StartListening();
        }

        // Stream audio mic ke Vosk
        private IEnumerator MicStreamCoroutine()
        {
            int sampleRate = 16000;
            int bufferSize = 4096;
            int lastSample = 0;

            _micClip = Microphone.Start(null, true, 10, sampleRate);

            while (_isListening)
            {
                if (_isPaused)
                {
                    yield return null;
                    continue;
                }

                int currentPos = Microphone.GetPosition(null);
                if (currentPos < lastSample) lastSample = 0;

                int sampleCount = currentPos - lastSample;
                if (sampleCount >= bufferSize)
                {
                    float[] samples = new float[sampleCount];
                    _micClip.GetData(samples, lastSample);

                    // Konversi float ke short untuk Vosk
                    short[] shorts = new short[samples.Length];
                    for (int i = 0; i < samples.Length; i++)
                        shorts[i] = (short)(samples[i] * 32767);

                    // Feed ke recognizer
                    bool isFinal = _recognizer.AcceptWaveform(shorts, shorts.Length);
                    if (isFinal)
                        OnResult(_recognizer.Result());
                    else
                        OnHypothesis(_recognizer.PartialResult());

                    lastSample = currentPos;
                }
                yield return null;
            }

            Microphone.End(null);
        }

        // Tampil preview hypothesis abu-abu
        private void OnHypothesis(string json)
        {
            if (_inputField == null) return;
            if (_inputField.isFocused && !_isListening) return;

            var data = JsonUtility.FromJson<VoskPartial>(json);
            if (data == null || string.IsNullOrEmpty(data.partial)) return;
            if (data.partial == _lastHypothesis) return;

            // Strip karakter non-standar dari hasil STT
            string cleaned = TextUtils.CleanSTTResult(data.partial);
            _lastHypothesis = data.partial;
            _inputField.text = cleaned;
            _inputField.textComponent.color = _hypothesisColor;

            // Reset timer setiap ada speech
            if (_silenceCoroutine != null)
                StopCoroutine(_silenceCoroutine);
            _silenceCoroutine = StartCoroutine(SilenceTimeoutCoroutine());
        }

        // Proses hasil final Vosk
        private void OnResult(string json)
        {
            if (_inputField == null) return;
            if (_inputField.isFocused && !_isListening) return;

            var data = JsonUtility.FromJson<VoskResult>(json);
            if (data == null || string.IsNullOrEmpty(data.text)) return;

            if (_silenceCoroutine != null)
            {
                StopCoroutine(_silenceCoroutine);
                _silenceCoroutine = null;
            }

            // Strip karakter non-standar dari hasil STT
            string cleaned = TextUtils.CleanSTTResult(data.text);
            _lastHypothesis = "";
            _inputField.text = cleaned;
            _inputField.textComponent.color = _normalColor;

            OnResultReady?.Invoke(cleaned);

            if (_autoSend)
                SendToChat(cleaned);
        }

        // Timeout diam otomatis kirim
        private IEnumerator SilenceTimeoutCoroutine()
        {
            yield return new WaitForSeconds(_silenceTimeout);

            string json = _recognizer.FinalResult();
            var data = JsonUtility.FromJson<VoskResult>(json);

            string finalText = (data != null && !string.IsNullOrEmpty(data.text))
                ? data.text
                : _lastHypothesis;

            if (!string.IsNullOrEmpty(finalText))
            {
                _lastHypothesis = "";

                if (_inputField != null)
                {
                    _inputField.text = finalText;
                    _inputField.textComponent.color = _normalColor;
                }

                OnResultReady?.Invoke(finalText);

                if (_autoSend)
                    SendToChat(finalText);
            }

            _silenceCoroutine = null;
        }

        // Kirim teks ke chat
        private void SendToChat(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (_inputField != null)
            {
                _inputField.text = text;
                _inputField.textComponent.color = _normalColor;
            }

            ChatUIManager.Instance?.OnUserSubmit();
        }

        // Bersihkan resource Vosk
        private void OnDestroy()
        {
            StopListening();
            _recognizer?.Dispose();
            _recognizer = null;

            if (Instance == this)
                Instance = null;
        }
    }
}
