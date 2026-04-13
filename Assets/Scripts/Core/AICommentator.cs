using System;
using System.Collections.Generic;
using UnityEngine;

public class AICommentator : MonoBehaviour
{
    // Singleton 
    public static AICommentator Instance { get; private set; }

    // Events 
    public event Action<GameEvent> OnCommentReady;

    // Nested Types 
    public enum EventPriority { LOW, NORMAL, HIGH }

    [Serializable]
    public class GameEvent
    {
        public string        gameName;
        public string        eventType;
        public string        details;
        public int           score;
        public EventPriority priority;

        public GameEvent(string gameName, string eventType, string details,
                         int score = 0, EventPriority priority = EventPriority.NORMAL)
        {
            this.gameName  = gameName;
            this.eventType = eventType;
            this.details   = details;
            this.score     = score;
            this.priority  = priority;
        }
    }

    // Inspector: Mini-game Commentary 

    // Inspector: Idle Attention 
    [Header("Idle Attention — Tier Thresholds")]
    [SerializeField] private float _tier1Delay = 10f;
    [SerializeField] private float _tier2Delay = 30f;
    [SerializeField] private float _tier3Delay = 60f;

    [Header("Idle Attention — System")]
    [SerializeField] private bool _idleActive = true;

    // Idle Prompt Templates 
    private static readonly string[] _tier1Prompts =
    {
        "User has been quiet for {0} seconds. Say something light and casual to check in - a short, friendly [NEUTRAL] comment.",
        "It has been {0} seconds since the user said anything. Give a gentle [NEUTRAL] nudge - keep it brief and warm.",
        "A brief silence of {0} seconds. Offer a relaxed [NEUTRAL] remark to re-engage the user naturally.",
        "The user seems idle - {0} seconds have passed. Make a small [NEUTRAL] observation or ask a simple open question.",
    };

    private static readonly string[] _tier2Prompts =
    {
        "User has been silent for {0} seconds. React with playful [SURPRISED] energy - you noticed their quiet, invite them back.",
        "Wow, {0} seconds without a word! Express [SURPRISED] concern and try to get the user's attention.",
        "The user has gone quiet for {0} seconds. React with [SURPRISED] energy - are they still there?",
        "{0} seconds of silence! Show a [SURPRISED] reaction and invite the user back into the conversation.",
    };

    private static readonly string[] _tier3Prompts =
    {
        "The user has been completely silent for {0} seconds. React with [SHY] vulnerability - maybe you miss them; say so gently.",
        "A full {0} seconds of silence. Express something [SHY] and quietly dramatic - it has been so long, where did they go?",
        "{0} seconds have passed without a word. Channel [SHY] loneliness - soft and earnest, not over the top.",
        "The user has disappeared for {0} seconds. Make a [SHY], slightly wistful comment about all this quiet.",
    };

    // Mini-game Commentary State 
    private readonly Queue<GameEvent> _commentQueue = new Queue<GameEvent>();
    public bool IsProcessing { get; set; }

    // Idle Attention State 
    private float _idleTimer    = 0f;
    private int   _idleTier     = 0;
    private bool  _idleTimerRunning = false;

    // Unity Lifecycle 
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleStateChanged;

        // Sync with current state if this component enables mid-session.
        if (GameManager.Instance != null)
            _idleTimerRunning = GameManager.Instance.CurrentState == GameManager.GameState.IDLE;
    }

    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleStateChanged;
        _idleTimerRunning = false;
    }

    private void Update()
    {
        // Mini-game commentary queue 
        if (!IsProcessing && _commentQueue.Count > 0)
            ProcessNextComment();

        // Idle attention timer 
        if (!_idleActive || !_idleTimerRunning) return;

        _idleTimer += Time.deltaTime;

        if (_idleTier < 1 && _idleTimer >= _tier1Delay)
        {
            _idleTier = 1;
            TriggerIdleComment(1);
        }
        else if (_idleTier < 2 && _idleTimer >= _tier2Delay)
        {
            _idleTier = 2;
            TriggerIdleComment(2);
        }
        else if (_idleTier < 3 && _idleTimer >= _tier3Delay)
        {
            _idleTier = 3;
            TriggerIdleComment(3);
            ResetIdleTimer(); 
        }
    }

    // Public API: Mini-game Commentary 
    public void TriggerComment(GameEvent gameEvent)
    {
        if (gameEvent.priority == EventPriority.LOW && _commentQueue.Count > 0)
        {
            // Debug.Log($"[AICommentator] LOW event dropped (queue busy): {gameEvent.eventType}");
            return;
        }

        if (gameEvent.priority == EventPriority.HIGH)
            CollapseHighPriorityEvents();

        _commentQueue.Enqueue(gameEvent);
        // Debug.Log($"[AICommentator] Enqueued [{gameEvent.priority}] {gameEvent.gameName}/{gameEvent.eventType} (queue: {_commentQueue.Count})");
    }

    // Public API: Idle Attention 
    public void ResetIdleTimer()
    {
        _idleTimer = 0f;
        _idleTier  = 0;
        // Debug.Log("[AICommentator] Idle timer reset.");
    }

    // State Machine Listener
    private void HandleStateChanged(GameManager.GameState prev, GameManager.GameState next)
    {
        switch (next)
        {
            case GameManager.GameState.IDLE:
                _idleTimerRunning = true;
                break;

            case GameManager.GameState.LISTENING:
            case GameManager.GameState.THINKING:
            case GameManager.GameState.SPEAKING:
            case GameManager.GameState.MINIGAME:
            case GameManager.GameState.MINIGAME_COMMENT:
            case GameManager.GameState.ERROR:

                _idleTimerRunning = false;
                break;
        }
    }

    // Private: Mini-game Commentary 
    private void ProcessNextComment()
    {
        if (_commentQueue.Count == 0) return;

        var evt = _commentQueue.Dequeue();
        Debug.Log($"[AICommentator] Comment: [{evt.priority}] {evt.gameName} - {evt.eventType} (score {evt.score})");

        IsProcessing = false;
        OnCommentReady?.Invoke(evt);
    }

    private void CollapseHighPriorityEvents()
    {
        var temp = new List<GameEvent>(_commentQueue);
        int highCount = 0;
        foreach (var e in temp)
            if (e.priority == EventPriority.HIGH) highCount++;

        if (highCount < 2) return;

        int lastHighIndex = -1;
        for (int i = temp.Count - 1; i >= 0; i--)
            if (temp[i].priority == EventPriority.HIGH) { lastHighIndex = i; break; }

        _commentQueue.Clear();
        for (int i = 0; i < temp.Count; i++)
            if (temp[i].priority != EventPriority.HIGH || i == lastHighIndex)
                _commentQueue.Enqueue(temp[i]);

        Debug.Log("[AICommentator] HIGH priority events collapsed - kept only newest.");
    }

    // Private: Idle Attention 
    private void TriggerIdleComment(int tier)
    {
        string[] pool = tier switch
        {
            1 => _tier1Prompts,
            2 => _tier2Prompts,
            3 => _tier3Prompts,
            _ => _tier1Prompts,
        };

        string template = pool[UnityEngine.Random.Range(0, pool.Length)];
        string prompt   = template.Replace("{0}", Mathf.RoundToInt(_idleTimer).ToString());

        // Debug.Log($"[AICommentator] Idle Tier {tier} - {_idleTimer:F1}s - \"{prompt}\"");

        GameManager.Instance?.ProcessUserInput(prompt);
    }
}
