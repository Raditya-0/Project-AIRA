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

    // Event asteroid hancur
    public event Action<Vector3> onAsteroidDestroyed;
    public void AsteroidDestroyed(Vector3 pos) => onAsteroidDestroyed?.Invoke(pos);

    // Event collectible muncul
    public event Action<Vector3> onCollectibleSpawned;
    public void CollectibleSpawned(Vector3 pos) => onCollectibleSpawned?.Invoke(pos);

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

    // Event asteroid hancur oleh penembak
    public event Action<Vector3, BulletOwner> onAsteroidDestroyedByShooter;
    public void AsteroidDestroyedByShooter(Vector3 pos, BulletOwner shooter)
        => onAsteroidDestroyedByShooter?.Invoke(pos, shooter);

    // Event ini akan didengarkan oleh Manager untuk mereset state game
    public event Action onRetry;

    public void OnRetry()
    {
        onRetry?.Invoke();
    }

    // Event respawn player selesai
    public event Action onPlayerRespawnEnd;
    public void PlayerRespawnEnd() => onPlayerRespawnEnd?.Invoke();

    // Event damage companion
    public event Action<float> onCompanionDamage;
    public void CompanionDamage(float amount) => onCompanionDamage?.Invoke(amount);

    // Event heal companion
    public event Action<float> onCompanionHeal;
    public void CompanionHeal(float amount) => onCompanionHeal?.Invoke(amount);

    // Event mati companion
    public event Action onCompanionDeath;
    public void CompanionDeath() => onCompanionDeath?.Invoke();

    // Event respawn companion selesai
    public event Action onCompanionRespawnEnd;
    public void CompanionRespawnEnd() => onCompanionRespawnEnd?.Invoke();
}
}