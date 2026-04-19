using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRA.Character
{
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

    // Mini-game Commentary State
    private readonly Queue<GameEvent> _commentQueue = new Queue<GameEvent>();
    public bool IsProcessing { get; set; }

    // Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        // Proses antrian komentar mini-game
        if (!IsProcessing && _commentQueue.Count > 0)
            ProcessNextComment();
    }

    // Public API: Mini-game Commentary
    public void TriggerComment(GameEvent gameEvent)
    {
        if (gameEvent.priority == EventPriority.LOW && _commentQueue.Count > 0)
            return;

        if (gameEvent.priority == EventPriority.HIGH)
            CollapseHighPriorityEvents();

        _commentQueue.Enqueue(gameEvent);
    }

    // Proses event berikutnya dari queue
    private void ProcessNextComment()
    {
        if (_commentQueue.Count == 0) return;

        var evt = _commentQueue.Dequeue();
        Debug.Log($"[AICommentator] Comment: [{evt.priority}] {evt.gameName} - {evt.eventType} (score {evt.score})");

        IsProcessing = false;
        OnCommentReady?.Invoke(evt);
    }

    // Pertahankan hanya HIGH priority terbaru
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
}
}
