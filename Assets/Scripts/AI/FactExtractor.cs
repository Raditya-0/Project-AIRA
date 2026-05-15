using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AIRA.AI
{
    public class FactExtractor
    {
        [Serializable]
        private class ExtractResult
        {
            public string       playerName;
            public List<string> newLikes;
            public List<string> newDislikes;
            public List<string> newMoments;
        }

        private const int TriggerInterval = 5;

        private int  _messageCount;
        private bool _isExtracting;
        private readonly Func<string, Task<string>> _llmCall;

        // Konstruktor dengan delegate LLM
        public FactExtractor(Func<string, Task<string>> llmCall)
        {
            _llmCall = llmCall;
        }

        // Proses tiap pesan masuk
        public async Task OnMessageAdded(string conversation)
        {
            _messageCount++;
            if (_messageCount < TriggerInterval) return;
            if (_isExtracting) return;

            _messageCount = 0;
            await ExtractFacts(conversation);
        }

        // Jalankan ekstraksi fakta
        private async Task ExtractFacts(string conversation)
        {
            _isExtracting = true;
            try
            {
                string prompt = BuildExtractionPrompt(conversation);
                string result = await _llmCall(prompt);
                ParseAndMerge(result);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FactExtractor] Extraction failed: {e.Message}");
            }
            finally
            {
                _isExtracting = false;
            }
        }

        // Bangun prompt ekstraksi fakta
        private string BuildExtractionPrompt(string conversation)
        {
            return
                "From this conversation, extract important facts about the player.\n" +
                "Reply ONLY with JSON format:\n" +
                "{\"playerName\":null,\"newLikes\":[],\"newDislikes\":[],\"newMoments\":[]}\n" +
                "If there are no new facts, all array fields should be empty.\n\n" +
                "Conversation:\n" + conversation;
        }

        // Parse dan gabung ke LongTermFacts
        private void ParseAndMerge(string json)
        {
            int start = json.IndexOf('{');
            int end   = json.LastIndexOf('}');
            if (start < 0 || end < start) return;

            string jsonPart = json.Substring(start, end - start + 1);

            ExtractResult result;
            try { result = JsonUtility.FromJson<ExtractResult>(jsonPart); }
            catch { return; }

            if (result == null) return;

            MemoryManager.LongTermFacts facts = MemoryManager.Instance?.Facts;
            if (facts == null) return;

            if (!string.IsNullOrEmpty(result.playerName))
                facts.playerName = result.playerName;

            if (result.newLikes != null)
                foreach (string like in result.newLikes)
                    if (!facts.likes.Contains(like)) facts.likes.Add(like);

            if (result.newDislikes != null)
                foreach (string dislike in result.newDislikes)
                    if (!facts.dislikes.Contains(dislike)) facts.dislikes.Add(dislike);

            if (result.newMoments != null)
                foreach (string moment in result.newMoments)
                    if (!facts.sharedMoments.Contains(moment)) facts.sharedMoments.Add(moment);

            Debug.Log("[FactExtractor] Fakta player berhasil diperbarui.");
        }
    }
}
