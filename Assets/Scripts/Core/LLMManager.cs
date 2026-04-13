using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if LLMUNITY_AVAILABLE
using LLMUnity;
#endif

public class LLMManager : MonoBehaviour
{
    // Singleton
    public static LLMManager Instance { get; private set; }

    // Events
    public event Action<string> OnResponseReceived;

    // Inspector
#if LLMUNITY_AVAILABLE
    [Header("LLMUnity")]
    [SerializeField] private LLMAgent _llmAgent;
#endif

    // Private State
    private CancellationTokenSource _cts;
    private bool _isReady;

    private AppSettings Settings => GameManager.Instance?.Settings;

    // Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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
#else
        _isReady = true;
        Debug.Log(
            "[LLMManager] Running in STUB mode. " +
            "Define LLMUNITY_AVAILABLE to connect LLMUnity.");
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
        "[HAPPY] Hei! Ini jawaban stub dari AIRA. LLMUnity belum terhubung nih!",
        "[THINKING] Hmm, aku lagi pura-pura mikir keras... Ini masih stub response ya!",
        "[NEUTRAL] Oke oke, aku dengar kamu! Tapi LLM-nya belum nyambung.",
        "[SHY] Eh, sebenarnya aku belum bisa jawab beneran... ini masih testing!",
        "[SURPRISED] Wah kamu nulis sesuatu! Sayang aku belum bisa baca beneran hehe.",
    };
}
