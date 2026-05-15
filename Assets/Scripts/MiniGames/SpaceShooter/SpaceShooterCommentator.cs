using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIRA.Character;
using AIRA.Voice;
using AIRA.AI;

namespace AIRA.MiniGames.SpaceShooter
{
    public class SpaceShooterCommentator : MonoBehaviour
    {
        // Singleton scene SpaceShooter
        public static SpaceShooterCommentator Instance { get; private set; }

        [Header("Companion Reference")]
        [SerializeField] private CompanionController m_companion;

        [Header("Player Reference")]
        [SerializeField] private Transform m_playerTransform;

        [Header("Comment Settings")]
        [SerializeField] private float m_commentCooldown = 8f;
        [SerializeField] private float m_urgentCooldown = 3f;

        [Header("Score Milestones")]
        [SerializeField] private int[] m_scoreMilestones = { 500, 1000 };
        [SerializeField] private int m_repeatMilestoneInterval = 5000;

        [Header("Health Thresholds")]
        [SerializeField] private float m_healthWarnThreshold = 0.5f;
        [SerializeField] private float m_healthCriticalThreshold = 0.2f;

        [Header("Saved Player Protection")]
        [SerializeField] private float m_savedByPlayerRadius = 4f;

        private float m_lastCommentTime;
        private float m_lastUrgentTime;
        private int m_lastCheckedScore;
        private int m_nextRepeatMilestone;
        private bool m_healthWarnTriggered;
        private bool m_healthCriticalTriggered;
        private HashSet<string> m_commentedEvents = new();

        // Awake singleton setup
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // Subscribe semua event
        private void OnEnable()
        {
            if (GameEvents.Instance == null) return;
            GameEvents.Instance.onPlayerDamage               += OnPlayerDamage;
            GameEvents.Instance.onPlayerDeath                += OnPlayerDeath;
            GameEvents.Instance.onPlayerHeal                 += OnPlayerHeal;
            GameEvents.Instance.onGameOver                   += OnGameOver;
            GameEvents.Instance.onAddToScore                 += OnScoreAdded;
            GameEvents.Instance.onAsteroidDestroyedByShooter += OnAsteroidDestroyedByShooter;

            if (m_companion != null)
            {
                m_companion.onNearMiss          += OnNearMiss;
                m_companion.onCollectiblePickup += OnCollectiblePickup;
            }
        }

        // Lepas semua event
        private void OnDisable()
        {
            if (GameEvents.Instance == null) return;
            GameEvents.Instance.onPlayerDamage               -= OnPlayerDamage;
            GameEvents.Instance.onPlayerDeath                -= OnPlayerDeath;
            GameEvents.Instance.onPlayerHeal                 -= OnPlayerHeal;
            GameEvents.Instance.onGameOver                   -= OnGameOver;
            GameEvents.Instance.onAddToScore                 -= OnScoreAdded;
            GameEvents.Instance.onAsteroidDestroyedByShooter -= OnAsteroidDestroyedByShooter;

            if (m_companion != null)
            {
                m_companion.onNearMiss          -= OnNearMiss;
                m_companion.onCollectiblePickup -= OnCollectiblePickup;
            }
        }

        // Cek cooldown health tiap frame
        private void Update()
        {
            if (HealthManager.Instance == null) return;
            float healthRatio = HealthManager.Instance.GetCurrentHealth() / HealthManager.Instance.GetMaxHealth();

            if (!m_healthCriticalTriggered && healthRatio < m_healthCriticalThreshold)
            {
                m_healthCriticalTriggered = true;
                m_healthWarnTriggered = true;
                TriggerComment("health_critical", true);
            }
            else if (!m_healthWarnTriggered && healthRatio < m_healthWarnThreshold)
            {
                m_healthWarnTriggered = true;
                TriggerComment("health_warning", true);
            }

            // Reset flag saat health pulih
            if (healthRatio > m_healthWarnThreshold)
            {
                m_healthWarnTriggered = false;
                m_healthCriticalTriggered = false;
            }
        }

        // Cek cooldown normal
        private bool CanComment() =>
            Time.time - m_lastCommentTime > m_commentCooldown;

        // Cek cooldown urgent
        private bool CanCommentUrgent() =>
            Time.time - m_lastUrgentTime > m_urgentCooldown;

        // Kirim komentar ke LLM
        private void TriggerComment(string eventType, bool urgent = false)
        {
            if (urgent && !CanCommentUrgent()) return;
            if (!urgent && !CanComment()) return;

            if (urgent) m_lastUrgentTime = Time.time;
            else m_lastCommentTime = Time.time;

            StartCoroutine(CommentRoutine(eventType));
        }

        // Bangun prompt dan kirim ke LLM
        private IEnumerator CommentRoutine(string eventType)
        {
            if (LLMManager.Instance == null) yield break;

            string prompt = BuildPrompt(eventType);
            var task = LLMManager.Instance.SendMessage(prompt);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.IsFaulted || task.IsCanceled) yield break;

            string response = task.Result;
            if (string.IsNullOrEmpty(response)) yield break;

            m_commentedEvents.Add(eventType);

            string expression = AiraController.ExtractExpressionTag(response);
            string clean = TextUtils.StripExpressionTags(response);

            TTSManager.Instance?.EnqueueSpeak(clean, expression.Trim('[', ']'));
        }

        // Format prompt untuk LLM
        private string BuildPrompt(string eventType)
        {
            string alreadySaid = string.Join(", ", m_commentedEvents);

            string basePrompt =
                "You are Aira, a cheerful AI companion co-piloting a space shooter with the player. " +
                "Keep responses SHORT — maximum 1-2 sentences. " +
                "Start with ONE expression tag: [HAPPY] [SAD] [SURPRISED] [THINKING] [SHY] [NEUTRAL]\n\n" +
                $"[EVENT] {eventType}\n" +
                $"[ALREADY COMMENTED] {alreadySaid}\n";

            string instruction = eventType switch
            {
                "player_damaged"        => "Player just got hit! React with concern. Short and snappy.",
                "player_died"           => "Player just died! React with shock and encouragement.",
                "player_healed"         => "Player picked up a collectible and healed! React positively.",
                "game_over"             => "Game over! React with sympathy but keep spirits up.",
                "near_miss"             => "Aira's ship just barely dodged an asteroid! React with relief or surprise. First person.",
                "companion_collected"   => "Aira picked up a collectible for the team! React proudly. First person.",
                "score_milestone"       => "Just hit a score milestone! React with excitement. Mention the achievement.",
                "health_warning"        => "Health is below 50%! Warn the player urgently but stay positive.",
                "health_critical"       => "Health is critically low, below 20%! React with urgency, ask for collectibles.",
                "saved_by_player"       => "The player just destroyed an asteroid that was about to hit Aira! React with gratitude. First person.",
                "aira_protecting_player"=> "Aira just destroyed an asteroid near the player, protecting them! React proudly. First person.",
                _                       => "React naturally to the current situation."
            };

            return basePrompt + instruction;
        }

        // Handler player kena damage
        private void OnPlayerDamage(float amount) => TriggerComment("player_damaged");

        // Handler player mati
        private void OnPlayerDeath() => TriggerComment("player_died", true);

        // Handler player heal
        private void OnPlayerHeal(float amount) => TriggerComment("player_healed");

        // Handler game over
        private void OnGameOver() => TriggerComment("game_over", true);

        // Handler near miss companion
        private void OnNearMiss() => TriggerComment("near_miss");

        // Handler companion ambil collectible
        private void OnCollectiblePickup() => TriggerComment("companion_collected");

        // Handler score bertambah — cek milestone
        private void OnScoreAdded(int score)
        {
            m_lastCheckedScore += score;

            // Cek milestone tetap (500, 1000)
            foreach (int milestone in m_scoreMilestones)
            {
                string key = $"milestone_{milestone}";
                if (m_lastCheckedScore >= milestone && !m_commentedEvents.Contains(key))
                {
                    m_commentedEvents.Add(key);
                    TriggerComment("score_milestone", true);
                    return;
                }
            }

            // Cek kelipatan 5000
            if (m_nextRepeatMilestone == 0)
                m_nextRepeatMilestone = m_repeatMilestoneInterval;

            if (m_lastCheckedScore >= m_nextRepeatMilestone)
            {
                m_nextRepeatMilestone += m_repeatMilestoneInterval;
                TriggerComment("score_milestone", true);
            }
        }

        // Handler asteroid hancur — deteksi saved/protecting
        private void OnAsteroidDestroyedByShooter(Vector3 asteroidPos, BulletOwner shooter)
        {
            if (m_companion == null) return;

            float distToCompanion = Vector2.Distance(asteroidPos, m_companion.transform.position);

            if (shooter == BulletOwner.Player && distToCompanion < m_savedByPlayerRadius)
            {
                // Player hancurkan asteroid dekat companion
                TriggerComment("saved_by_player");
            }
            else if (shooter == BulletOwner.Companion && m_playerTransform != null)
            {
                float distToPlayer = Vector2.Distance(asteroidPos, m_playerTransform.position);
                if (distToPlayer < m_savedByPlayerRadius)
                {
                    // Companion hancurkan asteroid dekat player
                    TriggerComment("aira_protecting_player");
                }
            }
        }
    }
}
