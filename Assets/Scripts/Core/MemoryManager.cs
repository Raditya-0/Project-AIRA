using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class MemoryManager : MonoBehaviour
{
    // Singleton
    public static MemoryManager Instance { get; private set; }

    // Nested Types
    [Serializable]
    public class Message
    {
        public string role;
        public string content;
        public int    tokenCount;

        public Message() { }

        public Message(string role, string content)
        {
            this.role       = role;
            this.content    = content;
            this.tokenCount = EstimateTokenCount(content);
        }

        public static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Mathf.Max(1, text.Length / 4);
        }
    }

    [Serializable]
    private class SessionData
    {
        public string        sessionSummary;
        public List<Message> history;
    }

    // Public State
    public List<Message> activeHistory  { get; private set; } = new List<Message>();
    public string        sessionSummary { get; private set; } = "";
    public string        systemPrompt   { get; private set; } = "";

    // Settings Shortcuts
    private AppSettings Settings         => GameManager.Instance?.Settings;
    private int         SlidingThreshold => Settings?.memory_sliding_threshold ?? 1500;
    private int         SummaryThreshold => Settings?.memory_summary_threshold ?? 1800;

    // Paths
    private string _sessionSavePath;

    // Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sessionSavePath = Path.Combine(Application.persistentDataPath, "aira_session.json");
    }

    private void Start()
    {
        LoadSystemPrompt();
    }

    // System Prompt
    private void LoadSystemPrompt()
    {
        string path = Settings?.system_prompt_path ?? "Assets/Data/system_prompt_template.txt";
        try
        {
            systemPrompt = File.ReadAllText(path);
            string name  = Settings?.character_name ?? "Aira";
            systemPrompt = systemPrompt.Replace("{character_name}", name);
            Debug.Log("[MemoryManager] System prompt loaded.");
        }
        catch (Exception e)
        {
            systemPrompt = "[SYSTEM] You are a helpful AI companion named Aira.";
            Debug.LogWarning($"[MemoryManager] Could not load system prompt: {e.Message}");
        }
    }

    // Public API
    public void AddMessage(string role, string content)
    {
        activeHistory.Add(new Message(role, content));
        TrimIfNeeded();
    }

    public string GetFullContext()
    {
        var sb = new StringBuilder();

        string summary = string.IsNullOrEmpty(sessionSummary)
            ? "(belum ada ringkasan)"
            : sessionSummary;

        string prompt = systemPrompt.Replace("{session_summary}", summary);
        sb.AppendLine(prompt);
        sb.AppendLine();

        foreach (var msg in activeHistory)
            sb.AppendLine($"{msg.role}: {msg.content}");

        return sb.ToString();
    }

    /// <summary>
    public void TrimIfNeeded()
    {
        int total = GetTotalTokens();
        if (total < SlidingThreshold) return;

        if (total >= SummaryThreshold)
        {
            CollapseOldestToSummary();
        }
        else
        {
            while (GetTotalTokens() > SlidingThreshold && activeHistory.Count > 1)
                activeHistory.RemoveAt(0);
        }

        Debug.Log($"[MemoryManager] History trimmed. Token estimate now: {GetTotalTokens()}");
    }
    public static int EstimateTokenCount(string text) => Message.EstimateTokenCount(text);

    // Persistence
    public void SaveSession()
    {
        try
        {
            var data = new SessionData
            {
                sessionSummary = sessionSummary,
                history        = new List<Message>(activeHistory)
            };
            File.WriteAllText(_sessionSavePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[MemoryManager] Session saved → {_sessionSavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MemoryManager] SaveSession failed: {e.Message}");
        }
    }

    public void LoadSession()
    {
        if (!File.Exists(_sessionSavePath))
        {
            Debug.Log("[MemoryManager] No saved session found — starting fresh.");
            return;
        }
        try
        {
            string json    = File.ReadAllText(_sessionSavePath);
            var    data    = JsonUtility.FromJson<SessionData>(json);
            sessionSummary = data.sessionSummary ?? "";
            activeHistory  = data.history        ?? new List<Message>();
            Debug.Log($"[MemoryManager] Session loaded ({activeHistory.Count} messages).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MemoryManager] LoadSession failed: {e.Message}");
        }
    }

    // Private Helpers
    private int GetTotalTokens()
    {
        int total = 0;
        foreach (var m in activeHistory) total += m.tokenCount;
        return total;
    }

    private void CollapseOldestToSummary()
    {
        int half = Mathf.Max(1, activeHistory.Count / 2);
        var oldest = activeHistory.GetRange(0, half);
        activeHistory.RemoveRange(0, half);

        var sb = new StringBuilder("Ringkasan percakapan sebelumnya: ");
        foreach (var m in oldest)
            sb.Append($"[{m.role}] {m.content} ");

        sessionSummary = sb.ToString().Trim();
        Debug.Log($"[MemoryManager] {half} messages collapsed into summary.");
    }
}
