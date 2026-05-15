using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using AIRA.Emotion;

#if LLMUNITY_AVAILABLE
using LLMUnity;
#endif

namespace AIRA.AI
{
    public class LLMManager : MonoBehaviour, ILLMProvider
    {
        // Singleton
        public static LLMManager Instance { get; private set; }

        // Events
        public event Action<string> OnResponseReceived;

        // Inspector
    #if LLMUNITY_AVAILABLE
        [Header("LLMUnity")]
        [SerializeField] private LLMAgent     _llmAgent;
        [SerializeField] private LLMCharacter _extractionCharacter;
    #endif

        // Properti ready LLM
        public bool IsReady => _isReady;

        // Private State
        private CancellationTokenSource _cts;
        private bool _isReady;
        private bool _isExtracting;
        private FactExtractor _factExtractor;

        // Instruksi emosi untuk system prompt
        private const string EmotionSystemInstruction =
            "\nWhen you see [PLAYER EMOTION DETECTED] in a message, use that information " +
            "to adjust your response tone and expression tags accordingly. " +
            "The 'Suggested tone' field tells you which expression to use. " +
            "Never explicitly mention that you detected their emotion — just naturally " +
            "respond in a way that matches their emotional state.";

        private AppSettings Settings => GameManager.Instance?.Settings;

        // Unity Lifecycle
        // Inisialisasi singleton dan FactExtractor
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _factExtractor = new FactExtractor(SendForExtraction);
        }

        private void Start()
        {
    #if LLMUNITY_AVAILABLE
            if (_llmAgent == null)
            {
                Debug.LogError(
                    "[LLMManager] LLMAgent component is not assigned in the Inspector. " +
                    "See Assets/Plugins/README_LLMUNITY.txt for setup instructions.");
                return;
            }

            _isReady = true;
            Debug.Log("[LLMManager] LLMUnity is ready.");
            // LLM siap dipakai
            LoadingGate.Instance?.SetLLMReady();
    #else
            _isReady = true;
            Debug.Log(
                "[LLMManager] Running in STUB mode. " +
                "Define LLMUNITY_AVAILABLE to connect LLMUnity.");
            // LLM siap dipakai
            LoadingGate.Instance?.SetLLMReady();
    #endif
        }

        private void OnDestroy() => CancelCurrent();

        // Public API
        public async Task<string> SendMessage(
            string fullContext,
            CancellationToken externalToken = default)
        {
            CancelCurrent();

            float timeoutSeconds = Settings?.llm_timeout_seconds ?? 15f;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            _cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                string response = await SendMessageInternal(fullContext, _cts.Token);
                OnResponseReceived?.Invoke(response);
                _ = TriggerExtraction();
                return response;
            }
            catch (OperationCanceledException) when (!externalToken.IsCancellationRequested)
            {
                // Internal timeout
                throw new TimeoutException(
                    $"[LLMManager] LLM did not respond within {timeoutSeconds}s.");
            }
        }

        public void CancelCurrent()
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        // Jalankan ekstraksi fakta background
        private async Task TriggerExtraction()
        {
            if (_factExtractor == null || _isExtracting) return;
            _isExtracting = true;
            try
            {
                string context = MemoryManager.Instance?.GetFullContext() ?? string.Empty;
                await _factExtractor.OnMessageAdded(context);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LLMManager] TriggerExtraction error: {e.Message}");
            }
            finally
            {
                _isExtracting = false;
            }
        }

        // Kirim prompt khusus ekstraksi
        private async Task<string> SendForExtraction(string prompt)
        {
    #if LLMUNITY_AVAILABLE
            if (_extractionCharacter == null)
            {
                Debug.LogWarning("[LLMManager] ExtractionCharacter belum di-assign di Inspector.");
                return string.Empty;
            }
            try
            {
                string result = await _extractionCharacter.Chat(prompt);
                result = result?.Trim() ?? string.Empty;
                Debug.Log($"[LLMManager] Hasil ekstraksi: {result}");
                return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LLMManager] SendForExtraction error: {e.Message}");
                return string.Empty;
            }
    #else
            await Task.Yield();
            Debug.Log("[LLMManager] SendForExtraction stub — ekstraksi dilewati.");
            return string.Empty;
    #endif
        }

        // Internal
        private async Task<string> SendMessageInternal(
            string fullContext,
            CancellationToken ct)
        {
    #if LLMUNITY_AVAILABLE
            if (!_isReady || _llmAgent == null)
            {
                Debug.LogWarning("[LLMManager] LLMAgent is not ready — returning stub fallback.");
                return StubResponses[UnityEngine.Random.Range(0, StubResponses.Length)];
            }

            string response = await _llmAgent.Chat(fullContext);
            return response?.Trim() ?? string.Empty;
    #else
            int delayMs = UnityEngine.Random.Range(1000, 2000);
            await Task.Delay(delayMs, ct);

            string stubResponse = StubResponses[UnityEngine.Random.Range(0, StubResponses.Length)];
            Debug.Log($"[LLMManager] STUB response ({delayMs} ms): {stubResponse}");
            return stubResponse;
    #endif
        }

        // Stub Data
        private static readonly string[] StubResponses =
        {
            "[HAPPY] Hey! This is a stub response from AIRA. LLMUnity is not connected yet!",
            "[THINKING] Hmm, pretending to think really hard... This is still a stub response!",
            "[NEUTRAL] Okay okay, I hear you! But the LLM is not connected yet.",
            "[SHY] Ehh, I can't actually answer for real yet... this is still testing!",
            "[SURPRISED] Oh you wrote something! Too bad I can't read it for real yet hehe.",
        };
    }
}
