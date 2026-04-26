using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIRA.Character;
using AIRA.Voice;
using AIRA.AI;

namespace AIRA.MiniGames.Platformer
{
    public class PlatformerCommentator : MonoBehaviour
    {
        // Singleton scene Platformer
        public static PlatformerCommentator Instance { get; private set; }

        [Header("References")]
        [SerializeField] private AiraVisionSystem _vision;
        [SerializeField] private AiraFollowSystem _follow;

        [Header("Comment Settings")]
        [SerializeField] private float _commentCooldown = 10f;

        [Header("Fall Comment Settings")]
        [SerializeField] private float _fallCommentCooldown = 5f;
        private float _lastFallCommentTime;

        private HashSet<string> _commentedObjects = new();
        private float           _lastCommentTime;
        private bool            _keySeenFirstTime;
        private bool            _endSeenFirstTime;

        // Awake singleton setup
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // Subscribe semua event
        private void OnEnable()
        {
            AiraFollowSystem.OnPlayerIdle20s     += OnPlayerIdle20s;
            AiraFollowSystem.OnPlayerIdle60s     += OnPlayerIdle60s;
            AiraFollowSystem.OnPlayerResumed     += OnPlayerResumed;
            AiraFollowSystem.OnPlayerFellIntoGap += HandlePlayerFell;
            AiraFollowSystem.OnAiraFellIntoGap   += HandleAiraFell;
            PlatformerGame.OnKeyCollected        += OnKeyCollected;
            PlatformerGame.OnEndReached          += OnEndReached;
        }

        // Lepas semua event
        private void OnDisable()
        {
            AiraFollowSystem.OnPlayerIdle20s     -= OnPlayerIdle20s;
            AiraFollowSystem.OnPlayerIdle60s     -= OnPlayerIdle60s;
            AiraFollowSystem.OnPlayerResumed     -= OnPlayerResumed;
            AiraFollowSystem.OnPlayerFellIntoGap -= HandlePlayerFell;
            AiraFollowSystem.OnAiraFellIntoGap   -= HandleAiraFell;
            PlatformerGame.OnKeyCollected        -= OnKeyCollected;
            PlatformerGame.OnEndReached          -= OnEndReached;
        }

        // Cek vision pertama kali setiap frame
        private void Update()
        {
            if (_vision == null) return;

            if (_vision.CanSeeKey && !_keySeenFirstTime)
            {
                _keySeenFirstTime = true;
                TriggerComment("key_spotted", true);
            }

            if (_vision.CanSeeEnd && !_endSeenFirstTime)
            {
                _endSeenFirstTime = true;
                TriggerComment("end_spotted", true);
            }
        }

        // Cek cooldown komentar
        private bool CanComment() =>
            Time.time - _lastCommentTime > _commentCooldown;

        // Baca toggle STT dari settings
        private bool STTEnabled =>
            AIRASettings.Instance?.PlatformerSTTEnabled ?? false;

        // Kirim komentar ke LLM dan TTS
        private void TriggerComment(string eventType,
            bool highPriority = false,
            string playerInput = "")
        {
            if (!highPriority && !CanComment()) return;
            _lastCommentTime = Time.time;
            StartCoroutine(CommentRoutine(eventType, playerInput));
        }

        // Bangun prompt dan kirim ke LLM
        private IEnumerator CommentRoutine(string eventType, string playerInput = "")
        {
            if (LLMManager.Instance == null) yield break;

            string prompt = BuildPrompt(eventType, playerInput);
            var task = LLMManager.Instance.SendMessage(prompt);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.IsCanceled) yield break;

            string response = task.Result;
            if (string.IsNullOrEmpty(response)) yield break;

            _commentedObjects.Add(eventType);

            string expression = AiraController.ExtractExpressionTag(response);
            string clean      = TextUtils.StripExpressionTags(response);

            TTSManager.Instance?.EnqueueSpeak(clean, expression.Trim('[', ']'));
        }

        // Format prompt untuk LLM
        private string BuildPrompt(string eventType, string playerInput = "")
        {
            string visionCtx   = _vision != null ? _vision.BuildVisionContext() : "No vision data.";
            string alreadySaid = string.Join(", ", _commentedObjects);
            string keyStatus   = PlatformerGame.Instance?.KeyCollected == true
                ? "already collected" : "not yet collected";

            string prompt =
                "You are Aira, a cheerful AI companion playing a cooperative platformer with the player. " +
                "You are exploring together — you only know what you can see.\n\n" +
                $"[VISION] {visionCtx}\n" +
                $"[KEY] {keyStatus}\n" +
                $"[EVENT] {eventType}\n" +
                $"[ALREADY COMMENTED] {alreadySaid}\n";

            if (!string.IsNullOrEmpty(playerInput))
                prompt += $"[PLAYER SAID] {playerInput}\n";

            string responseInstruction = eventType switch
            {
                "player_fell_gap" =>
                    "The player just fell into a gap! React with surprise and sympathy. Keep it short, 1-2 sentences. " +
                    "Start with ONE expression tag: [SURPRISED] or [SAD]",
                "aira_fell_gap" =>
                    "Aira just fell into a gap while following the player! React in first person, surprised or embarrassed. Keep it short, 1-2 sentences. " +
                    "Start with ONE expression tag: [SURPRISED] or [SHY]",
                _ =>
                    "Respond naturally in 1-2 sentences. " +
                    "Don't repeat what you already commented about. " +
                    "Start with ONE expression tag: [HAPPY] [SURPRISED] [THINKING] [SHY] [NEUTRAL]"
            };

            prompt += $"\n{responseInstruction}";
            return prompt;
        }

        // Handler idle 20 detik
        private void OnPlayerIdle20s()  => TriggerComment("player_idle_hint");

        // Handler idle 60 detik
        private void OnPlayerIdle60s()  => TriggerComment("player_idle_long", true);

        // Handler player bergerak lagi
        private void OnPlayerResumed()  => TriggerComment("player_resumed");

        // Handler key diambil
        private void OnKeyCollected()   => TriggerComment("key_collected", true);

        // Handler level selesai
        private void OnEndReached()     => TriggerComment("level_complete", true);

        // Dipanggil saat stacking terjadi
        public void OnStacking()        => TriggerComment("stacking", true);

        // Terima input player via STT
        public void OnPlayerSpeech(string input)
        {
            if (!STTEnabled) return;
            TriggerComment("player_speech", true, input);
        }

        // Handler player jatuh ke jurang
        private void HandlePlayerFell()
        {
            if (Time.time - _lastFallCommentTime < _fallCommentCooldown) return;
            _lastFallCommentTime = Time.time;
            TriggerComment("player_fell_gap", true);
        }

        // Handler Aira jatuh ke jurang
        private void HandleAiraFell()
        {
            if (Time.time - _lastFallCommentTime < _fallCommentCooldown) return;
            _lastFallCommentTime = Time.time;
            TriggerComment("aira_fell_gap", true);
        }
    }
}
