using UnityEngine;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class ScoreManager : MonoBehaviour
{
    [Header("Score Stats")]
    [SerializeField] private int m_score;
    [SerializeField] private int m_topScore;

    [Header("Lives Stats")]
    [SerializeField] private int m_maxLives = 3;
    [SerializeField] private int m_currentLives;

    [Header("Health Stats")]
    [SerializeField] private float m_maxHealth = 100f;
    [SerializeField] private float m_currentHealth;

    private void Awake()
    {
        ClearScore();
        // Inisialisasi nilai saat game mulai
        m_currentLives = m_maxLives;
        m_currentHealth = m_maxHealth;
    }

    public void AddScore(int amount = 1)
    {
        m_score += amount;
        if (m_topScore < m_score)
        {
            m_topScore = m_score;
        }
        Debug.Log($"Skor: {m_score} | Top: {m_topScore}");
    }

    public void LoseLife()
    {
        m_currentLives--;
        Debug.Log($"Nyawa Tersisa: {m_currentLives} / {m_maxLives}");

        if (m_currentLives <= 0)
        {
            m_currentLives = 0; // Agar UI tidak menampilkan angka negatif
            GameOver();
        }
    }

    public void TakePlayerDamage(float amount)
    {
        if (m_currentHealth <= 0) return;

        m_currentHealth -= amount;

        if (m_currentHealth <= 0)
        {
            m_currentHealth = 0;

            // Memicu event kematian yang akan didengar oleh ShipController.ExecuteDeathEvent
            GameEvents.Instance.PlayerDeath();

            // Reset darah untuk nyawa berikutnya jika masih ada nyawa sisa
            if (m_currentLives > 0)
            {
                m_currentHealth = m_maxHealth;
            }
        }
    }

    private void OnRetry()
    {
        // 1. Kembalikan waktu game ke normal
        Time.timeScale = 1f;

        // 2. Reset semua status ke kondisi awal
        m_currentLives = m_maxLives;
        m_currentHealth = m_maxHealth;
        ClearScore();

        Debug.Log("ScoreManager: Game direset.");
    }

    private void GameOver()
    {
        Debug.Log("GAME OVER!");
        GameEvents.Instance.TriggerGameOver();

        // Gunakan Invoke atau Coroutine untuk menunda penghentian waktu
        Invoke("FreezeGame", 0.5f);
    }

    private void FreezeGame()
    {
        Time.timeScale = 0f;
    }

    void OnEnable()
    {
        if (GameEvents.Instance != null)
        {
            GameEvents.Instance.onAddToScore += UpdateScoreUI;
            GameEvents.Instance.onPlayerDeath += LoseLife;
            GameEvents.Instance.onPlayerDamage += TakePlayerDamage;
            GameEvents.Instance.onRetry += OnRetry;
            GameEvents.Instance.onPlayerHeal += HealPlayer;
        }
    }

    void OnDisable()
    {
        if (GameEvents.Instance != null)
        {
            GameEvents.Instance.onAddToScore -= UpdateScoreUI;
            GameEvents.Instance.onPlayerDeath -= LoseLife;
            GameEvents.Instance.onPlayerDamage -= TakePlayerDamage;
            // Lepas langganan event retry
            GameEvents.Instance.onRetry -= OnRetry;
        }
    }

    void UpdateScoreUI(int amount)
    {
        AddScore(amount);
    }

    private void HealPlayer(float amount)
    {
        m_currentHealth = Mathf.Min(m_currentHealth + amount, m_maxHealth);
        Debug.Log($"Darah bertambah! Sekarang: {m_currentHealth}");
    }

    public float GetCurrentHealth()
    {
        return m_currentHealth;
    }

    // Tambahkan fungsi-fungsi ini di dalam class ScoreManager
    public void ClearScore()
    {
        m_score = 0;
    }

    public int GetCurrentLives()
    {
        return m_currentLives;
    }
}
}