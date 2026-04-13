using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // State Enum
    public enum GameState
    {
        IDLE,
        LISTENING,
        THINKING,
        SPEAKING,
        MINIGAME,
        MINIGAME_COMMENT,
        ERROR
    }

    // Singleton
    public static GameManager Instance { get; private set; }

    // Events
    public static event Action<GameState, GameState> OnStateChanged;

    // Public State
    public GameState CurrentState { get; private set; } = GameState.IDLE;
    public AppSettings Settings { get; private set; }

    [Header("Scene Dependencies")]
    [SerializeField] private ChatUIManager _chatUIManager;

    // Private
    private Coroutine _thinkingTimeoutCoroutine;
    private FallbackData _fallbackData;

    private const string SettingsPath = "Assets/Data/settings.json";

    // Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSettings();
    }

    private void Start()
    {
        LoadFallbackResponses();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Settings
    private void LoadSettings()
    {
        try
        {
            string json = File.ReadAllText(SettingsPath);
            Settings = JsonUtility.FromJson<AppSettings>(json);
            Debug.Log($"[GameManager] Settings loaded from {SettingsPath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] Could not load settings.json ({e.Message}). Using defaults.");
            Settings = new AppSettings();
        }
    }

    // Fallback Responses
    [Serializable]
    private class FallbackData
    {
        public string[] timeout         = new string[0];
        public string[] error           = new string[0];
        public string[] cancel          = new string[0];
        public string[] model_not_found = new string[0];
    }

    private void LoadFallbackResponses()
    {
        string path = Settings?.fallback_responses_path ?? "Assets/Data/fallback_responses.json";
        try
        {
            string json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<FallbackData>(json);
            if (data != null)
            {
                _fallbackData = data;
                Debug.Log("[GameManager] Loaded categorized fallback responses.");
                return;
            }
        }
        catch { /* file missing or malformed, use empty defaults */ }

        _fallbackData = new FallbackData();
        Debug.Log("[GameManager] Using built-in fallback responses.");
    }

    public string GetFallbackResponse(string category = "error")
    {
        string[] pool;
        switch (category)
        {
            case "timeout":       pool = _fallbackData?.timeout;         break;
            case "cancel":        pool = _fallbackData?.cancel;          break;
            case "model_not_found": pool = _fallbackData?.model_not_found; break;
            default:              pool = _fallbackData?.error;           break;
        }

        if (pool == null || pool.Length == 0)
            return "Oops, something went wrong! Can you try again?";

        return pool[UnityEngine.Random.Range(0, pool.Length)];
    }

    // State Machine
    public void ChangeState(GameState newState)
    {
        if (this == null) return;
        if (newState == CurrentState) return;

        GameState previous = CurrentState;
        CurrentState = newState;

        if (previous == GameState.THINKING)
            StopThinkingTimeout();

        if (newState == GameState.THINKING)
            StartThinkingTimeout();

        Debug.Log($"[GameManager] State: {previous} → {newState}");
        OnStateChanged?.Invoke(previous, newState);
    }

    // LLM Pipeline
    public void ProcessUserInput(string userInput)
    {
        if (this == null || Instance == null) return;

        if (CurrentState != GameState.IDLE && CurrentState != GameState.LISTENING)
        {
            Debug.LogWarning($"[GameManager] ProcessUserInput ignored — state is {CurrentState}.");
            return;
        }

        ChangeState(GameState.THINKING);

        if (this != null && gameObject.activeInHierarchy)
            StartCoroutine(ProcessLLMRequest(userInput));
    }

    private IEnumerator ProcessLLMRequest(string userInput)
    {
        MemoryManager.Instance?.AddMessage("user", userInput);

        string context = MemoryManager.Instance?.GetFullContext() ?? userInput;
        string response = null;

        if (LLMManager.Instance != null)
        {
            var task = LLMManager.Instance.SendMessage(context);
            while (!task.IsCompleted)
                yield return null;

            if (this == null) yield break;

            if (!task.IsFaulted && !task.IsCanceled)
                response = task.Result;
            else if (task.IsFaulted)
                Debug.LogWarning($"[GameManager] LLM faulted: {task.Exception?.InnerException?.Message}");
        }

        if (CurrentState != GameState.THINKING)
            yield break;

        if (string.IsNullOrEmpty(response))
            response = GetFallbackResponse("error");

        MemoryManager.Instance?.AddMessage("assistant", response);

        string expressionTag = AiraController.ExtractExpressionTag(response);
        AiraController.Instance?.SetExpression(expressionTag);

        string cleanText = AiraController.StripExpressionTags(response);
        _chatUIManager?.DisplayMessage("aira", cleanText);
        _chatUIManager?.ShowDialogBubble(cleanText, 4f);

        float speakDuration = Mathf.Clamp(cleanText.Length * 0.05f, 1.5f, 6f);
        ChangeState(GameState.SPEAKING);
        yield return new WaitForSeconds(speakDuration);
        ChangeState(GameState.IDLE);
    }

    // Thinking Timeout
    private void StartThinkingTimeout()
    {
        if (this == null || !gameObject.activeInHierarchy) return;
        StopThinkingTimeout();
        _thinkingTimeoutCoroutine = StartCoroutine(ThinkingTimeoutRoutine());
    }

    private void StopThinkingTimeout()
    {
        if (_thinkingTimeoutCoroutine != null)
        {
            StopCoroutine(_thinkingTimeoutCoroutine);
            _thinkingTimeoutCoroutine = null;
        }
    }

    private IEnumerator ThinkingTimeoutRoutine()
    {
        float timeout = Settings != null ? Settings.llm_timeout_seconds : 15f;
        yield return new WaitForSeconds(timeout);

        if (this == null) yield break;

        Debug.LogWarning($"[GameManager] THINKING timeout after {timeout}s.");

        LLMManager.Instance?.CancelCurrent();

        string fallback = GetFallbackResponse("timeout");
        _chatUIManager?.DisplayMessage("aira", fallback);
        _chatUIManager?.ShowDialogBubble(fallback, 4f);

        ChangeState(GameState.ERROR);
    }
}

// Settings Data Class
[Serializable]
public class AppSettings
{
    public string character_name            = "Aira";
    public string model_path               = "Assets/Models/qwen2.5-3b-q4.gguf";
    public float  llm_timeout_seconds      = 15f;
    public int    llm_max_tokens           = 256;
    public int    llm_context_limit        = 2000;
    public string tts_engine              = "piper";
    public string tts_voice_path          = "";
    public float  lipsync_min_rms         = 0.02f;
    public float  lipsync_max_rms         = 0.3f;
    public int    memory_summary_interval  = 20;
    public int    memory_sliding_threshold = 1500;
    public int    memory_summary_threshold = 1800;
    public float  idle_comment_interval_sec = 10f;
    public string fallback_responses_path  = "Assets/Data/fallback_responses.json";
    public string system_prompt_path       = "Assets/Data/system_prompt_template.txt";
    public float  autoblink_min_sec        = 2.0f;
    public float  autoblink_max_sec        = 6.0f;
}
