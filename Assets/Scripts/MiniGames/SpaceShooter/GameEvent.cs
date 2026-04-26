using System;
using UnityEngine;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class GameEvents
{
    private static GameEvents m_instance;

    public static GameEvents Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = new GameEvents();
            }
            return m_instance;
        }
    }

    public event Action<float> onPlayerHeal;

    public void PlayerHeal(float amount)
    {
        onPlayerHeal?.Invoke(amount);
    }
    // --- Score Events ---
    public event Action<int> onAddToScore;
    public void AddToScore(int amount) => onAddToScore?.Invoke(amount);

    // --- Player Status Events ---
    public event Action onPlayerDeath;
    public void PlayerDeath() => onPlayerDeath?.Invoke();

    public event Action<float> onPlayerDamage;
    public void PlayerDamage(float amount) => onPlayerDamage?.Invoke(amount);

    // --- Game Logic Events ---
    public event Action onGameOver;
    public void TriggerGameOver()
    {
        onGameOver?.Invoke();
    }

    // --- Retry & Reset Events (PENTING) ---
    // Event ini akan didengarkan oleh Manager untuk mereset state game
    public event Action onRetry;

    public void OnRetry()
    {
        onRetry?.Invoke();
    }
}
}