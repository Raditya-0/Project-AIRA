using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using AIRA.Character;

namespace AIRA.Voice
{
    public class TTSManager : MonoBehaviour, ITTSProvider
    {
        // Singleton
        public static TTSManager Instance { get; private set; }

        [Header("Piper Config")]
        [SerializeField] private string _piperExePath = "Piper/piper.exe";
        [SerializeField] private string _modelPath    = "Piper/voices/en_US-amy-medium.onnx";

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;

        [Header("Pitch per Ekspresi")]
        [SerializeField] private float _pitchNeutral   = 1.30f;
        [SerializeField] private float _pitchHappy     = 1.35f;
        [SerializeField] private float _pitchSad       = 1.20f;
        [SerializeField] private float _pitchSurprised = 1.40f;
        [SerializeField] private float _pitchShy       = 1.30f;
        [SerializeField] private float _pitchThinking  = 1.25f;

        [Header("Speed per Ekspresi (AudioSource.pitch juga affect speed)")]
        [SerializeField] private float _speedNeutral   = 1.00f;
        [SerializeField] private float _speedHappy     = 1.10f;
        [SerializeField] private float _speedSad       = 0.90f;
        [SerializeField] private float _speedSurprised = 1.15f;
        [SerializeField] private float _speedShy       = 0.95f;
        [SerializeField] private float _speedThinking  = 1.00f;

        [Header("Lip Sync")]
        [SerializeField] private float _lipSyncSensitivity = 8f;
        [SerializeField] private float _lipSyncSmoothing   = 0.1f;

        // Event mulai dan selesai bicara
        public event Action OnSpeakStart;
        public event Action OnSpeakEnd;

        // Properti status TTS aktif
        public bool IsSpeaking => _isSpeaking || _isProcessingQueue;

        // State internal
        private Coroutine _speakCoroutine;
        private Coroutine _lipSyncCoroutine;
        private float     _currentMouth      = 0f;
        private bool      _isSpeaking        = false;
        private string    _currentExpression = "NEUTRAL";

        // Queue pesan TTS
        private readonly Queue<(string text, string expression)> _speakQueue = new();
        private Coroutine _queueCoroutine;
        private bool      _isProcessingQueue     = false;
        private bool      _suppressSpeakEndEvent = false;

        // Path Piper resolved
        private string _resolvedPiperExe;
        private string _resolvedModelPath;

        // Daftar file temp aktif
        private readonly List<string> _activeTempFiles = new List<string>();

        // Unity Awake singleton
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Resolve path absolut Piper
            _resolvedPiperExe  = Path.Combine(Application.streamingAssetsPath, _piperExePath);
            _resolvedModelPath = Path.Combine(Application.streamingAssetsPath, _modelPath);
        }

        // Verifikasi path, warm-up Piper, report ready
        private void Start()
        {
            bool exeExists   = System.IO.File.Exists(_resolvedPiperExe);
            bool modelExists = System.IO.File.Exists(_resolvedModelPath);

            if (exeExists && modelExists)
            {
                StartCoroutine(WarmUpAndReady());
            }
            else
            {
                Debug.LogWarning("[TTSManager] Piper exe atau model tidak ditemukan — TTS tidak ready.");
            }
        }

        // Generate WAV dummy lalu report ready
        private IEnumerator WarmUpAndReady()
        {
            string warmUpPath = NewTempPath();
            var task = RunPiperAsync(".", warmUpPath);
            yield return new WaitUntil(() => task.IsCompleted);
            CleanupTempFile(warmUpPath);

            if (task.IsFaulted)
                Debug.LogWarning($"[TTSManager] Warm-up gagal: {task.Exception?.GetBaseException().Message}");
            else
                Debug.Log("[TTSManager] Warm-up selesai.");

            // TTS siap dipakai
            LoadingGate.Instance?.SetTTSReady();
        }

        // Daftar listener state
        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
        }

        // Lepas listener state
        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        // Bersih singleton OnDestroy
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Hentikan saat state berubah
        private void HandleStateChanged(GameManager.GameState prev, GameManager.GameState next)
        {
            if (next != GameManager.GameState.SPEAKING && _isSpeaking)
                StopSpeaking();
        }

        // Tambah ke queue TTS
        public void EnqueueSpeak(string text, string expression = "NEUTRAL")
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _speakQueue.Enqueue((text, expression));
            if (!_isProcessingQueue)
                _queueCoroutine = StartCoroutine(ProcessSpeakQueue());
        }

        // Proses queue berurutan
        private IEnumerator ProcessSpeakQueue()
        {
            _isProcessingQueue     = true;
            _suppressSpeakEndEvent = true;

            while (_speakQueue.Count > 0)
            {
                var (text, expression) = _speakQueue.Dequeue();
                _currentExpression = expression;
                _isSpeaking        = true;
                yield return StartCoroutine(SpeakCoroutine(ChunkBySentence(text)));
            }

            _suppressSpeakEndEvent = false;
            _isProcessingQueue     = false;
            _queueCoroutine        = null;

            // Fire event setelah queue benar-benar kosong
            OnSpeakEnd?.Invoke();
            if (GameManager.Instance?.CurrentState == GameManager.GameState.SPEAKING)
                GameManager.Instance.ChangeState(GameManager.GameState.IDLE);
        }

        // Terima teks dan ekspresi aktif
        public void Speak(string text, string expression = "NEUTRAL")
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            StopSpeaking();

            _currentExpression = expression;

            string[] chunks = ChunkBySentence(text);
            _isSpeaking     = true;
            _speakCoroutine = StartCoroutine(SpeakCoroutine(chunks));
        }

        // Hentikan semua TTS (ITTSProvider)
        public void StopAll() => StopSpeaking();

        // Ambil pitch sesuai ekspresi aktif
        private float GetPitchForExpression(string expression) => expression switch
        {
            "HAPPY"     => _pitchHappy,
            "SAD"       => _pitchSad,
            "SURPRISED" => _pitchSurprised,
            "SHY"       => _pitchShy,
            "THINKING"  => _pitchThinking,
            _           => _pitchNeutral
        };

        // Ambil speed sesuai ekspresi aktif
        private float GetSpeedForExpression(string expression) => expression switch
        {
            "HAPPY"     => _speedHappy,
            "SAD"       => _speedSad,
            "SURPRISED" => _speedSurprised,
            "SHY"       => _speedShy,
            "THINKING"  => _speedThinking,
            _           => _speedNeutral
        };

        // Potong per kalimat
        private string[] ChunkBySentence(string text)
        {
            var sentences = new List<string>();
            string current = "";

            for (int i = 0; i < text.Length; i++)
            {
                current += text[i];
                if (text[i] == '.' || text[i] == '!' || text[i] == '?')
                {
                    string trimmed = current.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        sentences.Add(trimmed);
                    current = "";
                }
            }

            // Sisa teks tanpa tanda baca
            if (!string.IsNullOrEmpty(current.Trim()))
                sentences.Add(current.Trim());

            // Gabung chunk terlalu pendek
            var results = new List<string>();
            string buffer = "";

            foreach (string s in sentences)
            {
                int words = s.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

                if (words < 3)
                {
                    buffer = string.IsNullOrEmpty(buffer) ? s : buffer + " " + s;
                }
                else
                {
                    if (!string.IsNullOrEmpty(buffer))
                    {
                        results.Add((buffer + " " + s).Trim());
                        buffer = "";
                    }
                    else
                    {
                        results.Add(s);
                    }
                }
            }

            // Tambah sisa buffer
            if (!string.IsNullOrEmpty(buffer))
                results.Add(buffer);

            return results.Count > 0 ? results.ToArray() : new[] { text };
        }

        // Pipeline chunk dengan pregenerate
        private IEnumerator SpeakCoroutine(string[] chunks)
        {
            // Generate chunk pertama sebelum loop
            string currentPath = NewTempPath();
            var currentTask    = RunPiperAsync(chunks[0], currentPath);
            yield return new WaitUntil(() => currentTask.IsCompleted);

            if (currentTask.IsFaulted)
            {
                Debug.LogWarning($"[TTSManager] Piper error chunk 0: {currentTask.Exception?.GetBaseException().Message}");
                CleanupTempFile(currentPath);
                FinishSpeaking();
                yield break;
            }

            for (int i = 0; i < chunks.Length; i++)
            {
                if (!_isSpeaking) break;

                // Mulai pregenerate chunk berikutnya
                string nextPath = null;
                System.Threading.Tasks.Task nextTask = null;

                if (i + 1 < chunks.Length)
                {
                    nextPath = NewTempPath();
                    nextTask = RunPiperAsync(chunks[i + 1], nextPath);
                }

                // Play chunk sekarang
                yield return PlayChunk(currentPath, isFirst: i == 0);

                // Tunggu chunk berikutnya selesai
                if (nextTask != null)
                    yield return new WaitUntil(() => nextTask.IsCompleted);

                if (nextTask != null && nextTask.IsFaulted)
                {
                    Debug.LogWarning($"[TTSManager] Piper error chunk {i + 1}: {nextTask.Exception?.GetBaseException().Message}");
                    CleanupTempFile(nextPath);
                    break;
                }

                currentPath = nextPath;
            }

            FinishSpeaking();
        }

        // Load dan play satu WAV
        private IEnumerator PlayChunk(string path, bool isFirst)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning("[TTSManager] WAV tidak ditemukan.");
                yield break;
            }

            string uri = "file:///" + path.Replace("\\", "/");
            using var req = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[TTSManager] Gagal load WAV: {req.error}");
                CleanupTempFile(path);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
            {
                CleanupTempFile(path);
                yield break;
            }

            // Fire event chunk pertama
            if (isFirst) OnSpeakStart?.Invoke();

            // Apply pitch sesuai ekspresi
            _audioSource.pitch = GetPitchForExpression(_currentExpression)
                               * GetSpeedForExpression(_currentExpression);
            _audioSource.PlayOneShot(clip);

            // Lip sync paralel
            if (_lipSyncCoroutine != null) StopCoroutine(_lipSyncCoroutine);
            _lipSyncCoroutine = StartCoroutine(LipSyncCoroutine());

            yield return new WaitForSeconds(clip.length / _audioSource.pitch);

            // Hapus file temp setelah diplay
            CleanupTempFile(path);
        }

        // Generate audio di background thread
        private System.Threading.Tasks.Task RunPiperAsync(string text, string outputPath)
        {
            RegisterTempFile(outputPath);
            return System.Threading.Tasks.Task.Run(() =>
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName              = _resolvedPiperExe;
                process.StartInfo.Arguments             = $"--model \"{_resolvedModelPath}\" --output_file \"{outputPath}\"";
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.UseShellExecute       = false;
                process.StartInfo.CreateNoWindow        = true;
                process.Start();
                process.StandardInput.WriteLine(text);
                process.StandardInput.Close();
                process.WaitForExit(); // blocking di background thread
            });
        }

        // Loop lip-sync saat audio play
        private IEnumerator LipSyncCoroutine()
        {
            float[] samples = new float[256];

            while (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.GetOutputData(samples, 0);
                float rms   = Mathf.Sqrt(samples.Average(s => s * s));
                float mouth = Mathf.Clamp01(rms * _lipSyncSensitivity);

                // Smooth interpolasi nilai mulut
                _currentMouth = Mathf.Lerp(
                    _currentMouth, mouth,
                    Time.deltaTime / _lipSyncSmoothing
                );

                AiraController.Instance?.SetMouthOpen(_currentMouth);
                yield return null;
            }

            // Reset mulut setelah audio selesai
            AiraController.Instance?.SetMouthOpen(0f);
            _currentMouth     = 0f;
            _lipSyncCoroutine = null;
        }

        // Stop semua + bersihkan queue
        public void StopSpeaking()
        {
            if (_queueCoroutine != null)
            {
                StopCoroutine(_queueCoroutine);
                _queueCoroutine = null;
            }
            _speakQueue.Clear();
            _isProcessingQueue     = false;
            _suppressSpeakEndEvent = false;

            if (_speakCoroutine != null)
            {
                StopCoroutine(_speakCoroutine);
                _speakCoroutine = null;
            }

            if (_lipSyncCoroutine != null)
            {
                StopCoroutine(_lipSyncCoroutine);
                _lipSyncCoroutine = null;
            }

            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();

            bool wasSpeaking = _isSpeaking;
            _isSpeaking   = false;
            _currentMouth = 0f;

            AiraController.Instance?.SetMouthOpen(0f);

            // Hapus semua file temp tersisa
            CleanupAllTempFiles();

            if (wasSpeaking) OnSpeakEnd?.Invoke();
        }

        // Selesai bicara satu item
        private void FinishSpeaking()
        {
            if (_lipSyncCoroutine != null)
            {
                StopCoroutine(_lipSyncCoroutine);
                _lipSyncCoroutine = null;
            }

            _isSpeaking     = false;
            _speakCoroutine = null;
            _currentMouth   = 0f;

            AiraController.Instance?.SetMouthOpen(0f);

            // Event dan state dihandle oleh queue, kecuali non-queue mode
            if (_suppressSpeakEndEvent) return;

            OnSpeakEnd?.Invoke();
            if (GameManager.Instance?.CurrentState == GameManager.GameState.SPEAKING)
                GameManager.Instance.ChangeState(GameManager.GameState.IDLE);
        }

        // Buat path temp unik
        private string NewTempPath()
        {
            return Path.Combine(
                Application.temporaryCachePath,
                $"tts_{Guid.NewGuid()}.wav"
            );
        }

        // Catat file temp aktif
        private void RegisterTempFile(string path)
        {
            lock (_activeTempFiles) _activeTempFiles.Add(path);
        }

        // Hapus satu file temp
        private void CleanupTempFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try   { if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning($"[TTSManager] Gagal hapus temp: {e.Message}"); }
            lock (_activeTempFiles) _activeTempFiles.Remove(path);
        }

        // Hapus semua file temp
        private void CleanupAllTempFiles()
        {
            lock (_activeTempFiles)
            {
                foreach (string path in _activeTempFiles)
                {
                    try   { if (File.Exists(path)) File.Delete(path); }
                    catch { /* abaikan error cleanup */ }
                }
                _activeTempFiles.Clear();
            }
        }
    }
}
