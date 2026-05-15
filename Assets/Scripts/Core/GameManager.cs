using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using AIRA.AI;
using AIRA.Character;
using AIRA.Core;
using AIRA.Emotion;
using AIRA.UI;
using AIRA.Voice;

public class GameManager : MonoBehaviour
{
    // State Enum
    public enum GameState
    {
        IDLE,
        LISTENING,
        THINKING,
        SPEAKING,
        MINIGAME_INTRO,     // flow awal: siapa duluan, kategori
        MINIGAME_PLAYING,   // game aktif
        MINIGAME_RESULT,    // AIRA comment hasil
        MINIGAME_PLATFORMER,    // platformer level aktif
        MINIGAME_SPACESHOOTER,  // space shooter aktif
        ERROR
    }

    // Singleton
    public static GameManager Instance { get; private set; }

    // Events
    public static event Action<GameState, GameState> OnStateChanged;

    // Public State
    public GameState CurrentState { get; private set; } = GameState.IDLE;
    public AppSettings Settings { get; private set; }
    public MiniGameBase CurrentMiniGame { get; private set; }

    // Private
    private Coroutine  _thinkingTimeoutCoroutine;
    private FallbackData _fallbackData;
    private GameState? _previousActiveState;

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

        // Simpan state aktif sebelum masuk THINKING/SPEAKING
        if ((newState == GameState.THINKING || newState == GameState.SPEAKING)
            && previous != GameState.THINKING && previous != GameState.SPEAKING)
            _previousActiveState = previous;

        // Bersihkan previous state saat kembali ke IDLE
        if (newState == GameState.IDLE)
            _previousActiveState = null;

        if (previous == GameState.THINKING)
            StopThinkingTimeout();

        if (newState == GameState.THINKING)
            StartThinkingTimeout();

        Debug.Log($"[GameManager] State: {previous} → {newState}");
        OnStateChanged?.Invoke(previous, newState);
    }

    // Kembali ke state sebelum THINKING/SPEAKING
    public void RestoreActiveState()
    {
        ChangeState(_previousActiveState ?? GameState.IDLE);
        _previousActiveState = null;
    }

    // Cek apakah minigame sedang aktif (termasuk saat THINKING/SPEAKING dalam minigame)
    public bool IsMinigameActive()
    {
        return CurrentState == GameState.MINIGAME_PLATFORMER
            || CurrentState == GameState.MINIGAME_SPACESHOOTER
            || CurrentState == GameState.MINIGAME_PLAYING
            || CurrentState == GameState.MINIGAME_INTRO
            || CurrentState == GameState.MINIGAME_RESULT
            || _previousActiveState == GameState.MINIGAME_PLATFORMER
            || _previousActiveState == GameState.MINIGAME_SPACESHOOTER
            || _previousActiveState == GameState.MINIGAME_PLAYING;
    }

    // Daftarkan minigame aktif
    public void RegisterMiniGame(MiniGameBase miniGame)
    {
        CurrentMiniGame = miniGame;
    }

    // Hapus registrasi minigame
    public void UnregisterMiniGame()
    {
        CurrentMiniGame = null;
    }

    // LLM Pipeline
    public void ProcessUserInput(string userInput)
    {
        if (this == null || Instance == null) return;

        Debug.Log($"[GameManager] ProcessUserInput called — CurrentState: {CurrentState}, int value: {(int)CurrentState}");

        // Delegasi input ke minigame aktif
        if (IsMinigameActive() && CurrentMiniGame != null && CurrentMiniGame.IsGameActive)
        {
            CurrentMiniGame.ProcessUserResponse(userInput);
            return;
        }

        if (CurrentState != GameState.IDLE
            && CurrentState != GameState.LISTENING
            && CurrentState != GameState.MINIGAME_PLATFORMER
            && CurrentState != GameState.MINIGAME_SPACESHOOTER)
        {
            Debug.LogWarning($"[GameManager] ProcessUserInput ignored — state is {CurrentState}.");
            return;
        }

        ChangeState(GameState.THINKING);

        if (this != null && gameObject.activeInHierarchy)
            StartCoroutine(ProcessLLMRequest(userInput));
    }

    // Kirim langsung ke pipeline LLM
    public void SendToLLM(string input)
    {
        if (this == null || !gameObject.activeInHierarchy) return;
        ChangeState(GameState.THINKING);
        StartCoroutine(ProcessLLMRequest(input));
    }

    private IEnumerator ProcessLLMRequest(string userInput)
    {
        MemoryManager.Instance?.AddMessage("user", userInput);

        string fullContext = MemoryManager.Instance?.GetFullContext() ?? userInput;

        // Klasifikasi emosi sebelum kirim ke LLM
        EmotionResult emotionResult = null;
        bool classifyDone = false;

        // Cek toggle AIRASettings dulu
        bool canClassify = AIRASettings.Instance != null
            && AIRASettings.Instance.UseEmotionClassifier
            && EmotionClassifier.Instance != null
            && EmotionClassifier.Instance.IsReady;

        if (canClassify)
        {
            EmotionClassifier.Instance.Classify(userInput, result =>
            {
                emotionResult = result;
                classifyDone  = true;
            });
            yield return new WaitUntil(() => classifyDone);
        }

        string context = BuildEmotionContext(fullContext, emotionResult);
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

        response = TextUtils.StripEmoji(response);

        MemoryManager.Instance?.AddMessage("assistant", response);

        string expressionTag = AiraController.ExtractExpressionTag(response);
        AiraController.Instance?.SetExpression(expressionTag);

        string cleanText = AiraController.StripExpressionTags(response);
        ChatUIManager.Instance?.DisplayMessageChunked("aira", cleanText);
        FindFirstObjectByType<AiraFloatingBubble>()?.ShowDialogBubble(cleanText, 4f);

        ChangeState(GameState.SPEAKING);

        if (TTSManager.Instance != null)
        {
            // Ambil tag ekspresi dari response
            string expression = expressionTag.Trim('[', ']');
            TTSManager.Instance.Speak(cleanText, expression);
        }
        else
        {
            // Fallback timer jika TTS tidak tersedia
            float speakDuration = Mathf.Clamp(cleanText.Length * 0.05f, 1.5f, 6f);
            yield return new WaitForSeconds(speakDuration);
            RestoreActiveState();
        }
    }

    // Inject emotion context ke LLM input
    private string BuildEmotionContext(string originalText, EmotionResult emotion)
    {
        bool skip = emotion == null
            || (emotion.dominantEmotion == "neutral" && emotion.confidence < 0.6f)
            || EmotionClassifier.Instance == null
            || !EmotionClassifier.Instance.IsReady;

        if (skip) return originalText;

        string secondary = emotion.top3 != null && emotion.top3.Count > 1
            ? $"{emotion.top3[1].emotion} ({emotion.top3[1].confidence:P0})"
            : "-";

        return
            $"[PLAYER EMOTION DETECTED]\n" +
            $"Dominant: {emotion.dominantEmotion} ({emotion.confidence:P0})\n" +
            $"Secondary: {secondary}\n" +
            $"Suggested tone: {emotion.airaHint}\n\n" +
            originalText;
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
        ChatUIManager.Instance?.DisplayMessage("aira", fallback);
        FindFirstObjectByType<AiraFloatingBubble>()?.ShowDialogBubble(fallback, 4f);
        AiraController.Instance?.SetExpression("[NEUTRAL]");
        if (TTSManager.Instance != null)
            TTSManager.Instance.Speak(fallback, "NEUTRAL");

        ChangeState(GameState.SPEAKING);
    }

    // Mulai scene Platformer
    public void StartPlatformer()
    {
        TTSManager.Instance?.StopSpeaking();
        Time.timeScale = 1f;
        ChangeState(GameState.MINIGAME_PLATFORMER);
        SceneManager.LoadScene("Platformer_Level01");
    }

    // Kembali ke scene utama
    public void EndPlatformer()
    {
        TTSManager.Instance?.StopSpeaking();
        STTManager.Instance?.StopListening();
        _previousActiveState = null;
        Time.timeScale = 1f;
        ChangeState(GameState.IDLE);
        SceneManager.LoadScene("MainScene");
    }

    // Mulai scene SpaceShooter
    public void StartSpaceShooter()
    {
        TTSManager.Instance?.StopSpeaking();
        ChangeState(GameState.MINIGAME_SPACESHOOTER);
        SceneManager.LoadScene("SpaceShooter");
    }

    // Kembali dari SpaceShooter
    public void EndSpaceShooter()
    {
        TTSManager.Instance?.StopSpeaking();
        STTManager.Instance?.StopListening();
        _previousActiveState = null;
        Time.timeScale = 1f;
        ChangeState(GameState.IDLE);
        SceneManager.LoadScene("MainScene");
    }

    // Mulai HeadsUp di scene aktif
    public void StartHeadsUp()
    {
        TTSManager.Instance?.StopSpeaking();
        HeadsUpGame.Instance?.StartGame();
    }

    // Tampilkan reaksi Aira tanpa LLM
    public void ProcessAiraReaction(string message)
    {
        if (CurrentState != GameState.IDLE && CurrentState != GameState.LISTENING) return;
        ChatUIManager.Instance?.DisplayMessage("aira", message);
        FindFirstObjectByType<AiraFloatingBubble>()?.ShowDialogBubble(message, 3f);
        AiraController.Instance?.SetExpression("[NEUTRAL]");
        if (TTSManager.Instance != null)
            TTSManager.Instance.Speak(message, "NEUTRAL");
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
