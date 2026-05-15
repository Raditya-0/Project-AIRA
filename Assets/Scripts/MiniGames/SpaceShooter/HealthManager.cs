using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter
{
    public class HealthManager : MonoBehaviour
    {
        // Singleton akses global
        public static HealthManager Instance { get; private set; }

        [Header("Player Health")]
        [SerializeField] private float m_playerMaxHealth = 100f;
        [SerializeField] private float m_playerCurrentHealth;

        [Header("Aira Health")]
        [SerializeField] private float m_airaMaxHealth = 100f;
        [SerializeField] private float m_airaCurrentHealth;

        // Inisialisasi singleton dan health
        private void Awake()
        {
            Instance              = this;
            m_playerCurrentHealth = m_playerMaxHealth;
            m_airaCurrentHealth   = m_airaMaxHealth;
        }

        // Daftarkan event damage dan heal
        private void OnEnable()
        {
            if (GameEvents.Instance == null) return;
            GameEvents.Instance.onPlayerDamage    += TakePlayerDamage;
            GameEvents.Instance.onPlayerHeal      += HealPlayer;
            GameEvents.Instance.onCompanionDamage += TakeAiraDamage;
            GameEvents.Instance.onCompanionHeal   += HealAira;
            GameEvents.Instance.onRetry           += OnRetry;
        }

        // Lepas event saat nonaktif
        private void OnDisable()
        {
            if (GameEvents.Instance == null) return;
            GameEvents.Instance.onPlayerDamage    -= TakePlayerDamage;
            GameEvents.Instance.onPlayerHeal      -= HealPlayer;
            GameEvents.Instance.onCompanionDamage -= TakeAiraDamage;
            GameEvents.Instance.onCompanionHeal   -= HealAira;
            GameEvents.Instance.onRetry           -= OnRetry;
        }

        // Proses damage player
        public void TakePlayerDamage(float amount)
        {
            if (m_playerCurrentHealth <= 0) return;
            m_playerCurrentHealth -= amount;
            if (m_playerCurrentHealth > 0) return;

            m_playerCurrentHealth = 0;
            GameEvents.Instance.PlayerDeath();

            // Reset health jika masih ada nyawa
            if (ScoreManager.Instance != null && ScoreManager.Instance.GetCurrentLives() > 0)
                m_playerCurrentHealth = m_playerMaxHealth;
        }

        // Tambah health saat heal
        public void HealPlayer(float amount)
        {
            m_playerCurrentHealth = Mathf.Min(m_playerCurrentHealth + amount, m_playerMaxHealth);
            Debug.Log($"Darah bertambah: {m_playerCurrentHealth}");
        }

        // Proses damage Aira
        public void TakeAiraDamage(float amount)
        {
            m_airaCurrentHealth = Mathf.Max(0f, m_airaCurrentHealth - amount);
            if (m_airaCurrentHealth <= 0f)
                GameEvents.Instance.CompanionDeath();
        }

        // Tambah health Aira
        public void HealAira(float amount)
        {
            m_airaCurrentHealth = Mathf.Min(m_airaCurrentHealth + amount, m_airaMaxHealth);
        }

        // Reset keduanya saat retry
        private void OnRetry()
        {
            m_playerCurrentHealth = m_playerMaxHealth;
            m_airaCurrentHealth   = m_airaMaxHealth;
        }

        // Getter health player saat ini
        public float GetCurrentHealth() => m_playerCurrentHealth;

        // Getter max health player
        public float GetMaxHealth() => m_playerMaxHealth;

        // Getter health Aira saat ini
        public float GetAiraCurrentHealth() => m_airaCurrentHealth;

        // Getter max health Aira
        public float GetAiraMaxHealth() => m_airaMaxHealth;
    }
}
