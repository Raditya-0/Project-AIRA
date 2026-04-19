using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AIRA.AI
{
    public class MemoryManager : MonoBehaviour, IMemoryProvider
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

            // Estimasi jumlah token
            public static int EstimateTokenCount(string text)
            {
                if (string.IsNullOrEmpty(text)) return 0;
                return Mathf.Max(1, text.Length / 4);
            }
        }

        [Serializable]
        public class LongTermFacts
        {
            public string       playerName;
            public List<string> likes;
            public List<string> dislikes;
            public List<string> sharedMoments;

            public LongTermFacts()
            {
                likes         = new List<string>();
                dislikes      = new List<string>();
                sharedMoments = new List<string>();
            }
        }

        [Serializable]
        public class SessionSummaryEntry
        {
            public string date;
            public string summary;
        }

        [Serializable]
        private class SessionData
        {
            public string        sessionSummary;
            public List<Message> history;
        }

        [Serializable]
        private class LongTermData
        {
            public LongTermFacts             longTermFacts;
            public List<SessionSummaryEntry> sessionSummaries;
        }

        // Public State
        public List<Message>  activeHistory  { get; private set; } = new List<Message>();
        public string         sessionSummary { get; private set; } = "";
        public string         systemPrompt   { get; private set; } = "";
        public LongTermFacts  Facts          { get; private set; } = new LongTermFacts();

        // Settings Shortcuts
        private AppSettings Settings         => GameManager.Instance?.Settings;
        private int         SlidingThreshold => Settings?.memory_sliding_threshold ?? 1500;
        private int         SummaryThreshold => Settings?.memory_summary_threshold ?? 1800;

        // Paths
        private string _sessionSavePath;
        private string SavePath => Path.Combine(Application.persistentDataPath, "aira_memory.json");

        // Private State
        private List<SessionSummaryEntry> _sessionSummaries = new List<SessionSummaryEntry>();

        // Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sessionSavePath = Path.Combine(Application.persistentDataPath, "aira_session.json");
            LoadMemory();
        }

        private void Start()
        {
            LoadSystemPrompt();
        }

        // Simpan memori saat keluar
        private void OnApplicationQuit()
        {
            SaveMemory();
        }

        // Muat system prompt
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
        // Tambah pesan ke history
        public void AddMessage(string role, string content)
        {
            activeHistory.Add(new Message(role, content));
            TrimIfNeeded();
        }

        // Bangun konteks lengkap prompt
        public string GetFullContext()
        {
            var sb = new StringBuilder();

            string summary = string.IsNullOrEmpty(sessionSummary)
                ? "(belum ada ringkasan)"
                : sessionSummary;

            string prompt = systemPrompt.Replace("{session_summary}", summary);
            sb.AppendLine(prompt);
            sb.AppendLine();

            string longTermContext = BuildLongTermContext();
            if (!string.IsNullOrEmpty(longTermContext))
            {
                sb.AppendLine(longTermContext);
                sb.AppendLine();
            }

            foreach (var msg in activeHistory)
                sb.AppendLine($"{msg.role}: {msg.content}");

            return sb.ToString();
        }

        // Pangkas history bila penuh
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

        // Estimasi token teks
        public static int EstimateTokenCount(string text) => Message.EstimateTokenCount(text);

        // Persistence
        // Simpan memori jangka panjang
        public void SaveMemory()
        {
            try
            {
                var data = new LongTermData
                {
                    longTermFacts    = Facts,
                    sessionSummaries = _sessionSummaries
                };
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                Debug.Log($"[MemoryManager] Memory saved → {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MemoryManager] SaveMemory failed: {e.Message}");
            }
        }

        // Muat memori jangka panjang
        public void LoadMemory()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[MemoryManager] No long-term memory found — starting fresh.");
                return;
            }
            try
            {
                string json = File.ReadAllText(SavePath);
                var data    = JsonUtility.FromJson<LongTermData>(json);
                if (data.longTermFacts    != null) Facts             = data.longTermFacts;
                if (data.sessionSummaries != null) _sessionSummaries = data.sessionSummaries;
                Debug.Log("[MemoryManager] Long-term memory loaded.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MemoryManager] LoadMemory failed: {e.Message}");
            }
        }

        // Bangun konteks fakta player
        public string BuildLongTermContext()
        {
            bool hasName     = !string.IsNullOrEmpty(Facts.playerName);
            bool hasLikes    = Facts.likes         != null && Facts.likes.Count         > 0;
            bool hasDislikes = Facts.dislikes      != null && Facts.dislikes.Count      > 0;
            bool hasMoments  = Facts.sharedMoments != null && Facts.sharedMoments.Count > 0;

            if (!hasName && !hasLikes && !hasDislikes && !hasMoments)
                return string.Empty;

            var sb = new StringBuilder("[FAKTA TENTANG PEMAIN]");

            if (hasName)
                sb.AppendLine().Append($"- Nama: {Facts.playerName}");

            if (hasLikes)
                sb.AppendLine().Append($"- Suka: {string.Join(", ", Facts.likes)}");

            if (hasDislikes)
                sb.AppendLine().Append($"- Tidak suka: {string.Join(", ", Facts.dislikes)}");

            if (hasMoments)
                sb.AppendLine().Append($"- Momen bersama: {string.Join(", ", Facts.sharedMoments)}");

            // Tambah instruksi penggunaan fakta
            sb.AppendLine().AppendLine()
              .AppendLine("CARA MENGGUNAKAN FAKTA INI:")
              .AppendLine("- Gunakan fakta secara natural dan subtle, bukan dipaksakan")
              .AppendLine("- Jangan sebut atau ungkit fakta ini di kalimat pembuka/greeting")
              .AppendLine("- Hanya pakai fakta kalau topik percakapan memang relevan")
              .AppendLine("- Biarkan player yang bawa topiknya duluan")
              .AppendLine("- Jangan pernah bilang \"aku ingat kamu suka X\" secara eksplisit")
              .Append    ("- Fakta ini untuk memahami player, bukan untuk diucapkan ulang");

            return sb.ToString();
        }

        // Simpan sesi saat ini
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
}
