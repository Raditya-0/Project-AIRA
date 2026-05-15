using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIRA.Character;
using AIRA.Core;
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
        [SerializeField] private float _commentCooldown = 25f;

        [Header("Fall Comment Settings")]
        [SerializeField] private float _fallCommentCooldown = 10f;
        private float _lastFallCommentTime;

        private HashSet<string> _commentedObjects = new();
        private readonly HashSet<string> _firedEvents  = new();
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
            AiraFollowSystem.OnPlayerIdle60s     += OnPlayerIdle60s;
            AiraFollowSystem.OnPlayerFellIntoGap += HandlePlayerFell;
            AiraFollowSystem.OnAiraFellIntoGap   += HandleAiraFell;
            PlatformerGame.OnKeyCollected        += OnKeyCollected;
            PlatformerGame.OnEndReached          += OnEndReached;
        }

        // Lepas semua event
        private void OnDisable()
        {
            AiraFollowSystem.OnPlayerIdle60s     -= OnPlayerIdle60s;
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
            bool isFallEvent = eventType == "player_fell_gap"
                            || eventType == "aira_fell_gap";

            if (!isFallEvent && _firedEvents.Contains(eventType)) return;
            if (!highPriority && !CanComment()) return;

            _firedEvents.Add(eventType);
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
                "aira_going_to_plate" =>
                    "You are walking to the pressure plate so the player can pass through the door. " +
                    "Tell the player in 1-2 sentences to go through the door first while you hold the plate. " +
                    "Start with ONE expression tag: [HAPPY] or [THINKING]",
                "aira_holding_plate" =>
                    "You are standing on the pressure plate and holding it down. " +
                    "Excitedly tell the player to go to the endpoint now. 1 short sentence. " +
                    "Start with ONE expression tag: [HAPPY]",
                "coop_success" =>
                    "You both just completed the cooperative section together! Celebrate the teamwork in 1 sentence. " +
                    "Start with ONE expression tag: [HAPPY]",
                "aira_frustrated_ignored" =>
                    "The player walked right past the pressure plate without stopping. " +
                    "React with slight passive-aggressive frustration, but keep it cute and short. " +
                    "Start with ONE expression tag: [SAD] or [SURPRISED]",
                "aira_frustrated_abandoned" =>
                    "The player has left the pressure plate area multiple times now. " +
                    "React more emotionally — more frustrated than before. 1-2 sentences. " +
                    "Start with ONE expression tag: [SAD]",
                "aira_hint_comeback" =>
                    "Gently hint to the player to come back to the pressure plate. Not angry, just a soft reminder. 1 sentence. " +
                    "Start with ONE expression tag: [THINKING]",
                "level_transition_2" =>
                    "You're about to move to the next area with the player. " +
                    "Say something short and excited about going together to the next challenge. " +
                    "1 sentence max. Start with [HAPPY]",
                "level_transition_3" =>
                    "This is the final area. Say something meaningful about how far you've come together. " +
                    "1 sentence max. Start with [HAPPY] or [NEUTRAL]",
                _ =>
                    "Respond naturally in 1-2 sentences. " +
                    "Don't repeat what you already commented about. " +
                    "Start with ONE expression tag: [HAPPY] [SURPRISED] [THINKING] [SHY] [NEUTRAL]"
            };

            prompt += $"\n{responseInstruction}";
            return prompt;
        }

        // Handler idle 60 detik
        private void OnPlayerIdle60s()  => TriggerComment("player_idle_long", true);

        // Handler key diambil
        private void OnKeyCollected()   => TriggerComment("key_collected", true);

        // Handler level selesai
        private void OnEndReached()     => TriggerComment("level_complete", true);

        // Dipanggil saat stacking terjadi
        public void OnStacking()        => TriggerComment("stacking", true);

        // Aira mulai jalan ke plate
        public void OnAiraGoingToPlate()        => TriggerComment("aira_going_to_plate");

        // Aira sudah tahan plate
        public void OnAiraHoldingPlate()        => TriggerComment("aira_holding_plate");

        // Kerja sama coop berhasil
        public void OnCoopSuccess()             => TriggerComment("coop_success", true);

        // Aira frustrasi diabaikan player
        public void OnAiraFrustratedIgnored()   => TriggerComment("aira_frustrated_ignored", true);

        // Aira frustrasi ditinggal berkali-kali
        public void OnAiraFrustratedAbandoned() => TriggerComment("aira_frustrated_abandoned", true);

        // Hint halus balik ke plate
        public void OnAiraHintComeback()        => TriggerComment("aira_hint_comeback");

        // Terima input player via STT
        public void OnPlayerSpeech(string input)
        {
            if (!STTEnabled) return;
            TriggerComment("player_speech", true, input);
        }

        // Komentar sebelum pindah level
        public void OnLevelTransition(int nextLevel)
        {
            string eventType = $"level_transition_{nextLevel}";
            TriggerComment(eventType, true);
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
