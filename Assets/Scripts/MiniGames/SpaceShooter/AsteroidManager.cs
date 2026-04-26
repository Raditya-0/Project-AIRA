using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class AsteroidManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private List<GameObject> m_bigAsteroidPrefabs;
    [SerializeField] private Rect m_spawnArea;
    [SerializeField] private int m_minBigAsteroids = 3;

    [Header("Safety")]
    [SerializeField] private Transform m_playerTransform;
    [SerializeField] private float m_safeDistance = 7f;

    private void Start()
    {
        if (m_playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) m_playerTransform = player.transform;
        }
        StartCoroutine(ManageAsteroidPopulation());
    }

    private void OnEnable()
    {
        if (GameEvents.Instance != null) GameEvents.Instance.onRetry += OnRetry;
    }

    private void OnDisable()
    {
        if (GameEvents.Instance != null) GameEvents.Instance.onRetry -= OnRetry;
    }

    private IEnumerator ManageAsteroidPopulation()
    {
        while (true)
        {
            AsteroidController[] allAsteroids = Object.FindObjectsByType<AsteroidController>(FindObjectsSortMode.None);
            int bigAsteroidCount = 0;

            foreach (var asteroid in allAsteroids)
            {
                // FIX: Menghapus tanda kurung ganda dan memastikan objek tidak null
                if (asteroid != null && asteroid.IsBigAsteroid())
                {
                    bigAsteroidCount++;
                }
            }

            if (bigAsteroidCount < m_minBigAsteroids)
            {
                float randomDelay = Random.Range(0.5f, 1f);
                yield return new WaitForSeconds(randomDelay);
                SpawnBigAsteroid();
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private void SpawnBigAsteroid()
    {
        if (m_bigAsteroidPrefabs.Count == 0) return;

        Vector2 spawnPoint = Vector2.zero;
        bool isSafe = false;
        int safetyCheckAttempts = 0;
        float asteroidSafetyRadius = 2.5f;

        while (!isSafe && safetyCheckAttempts < 25)
        {
            // LOGIKA BARU: Ambil posisi acak di dalam seluruh area m_spawnArea
            float randomX = Random.Range(m_spawnArea.xMin, m_spawnArea.xMax);
            float randomY = Random.Range(m_spawnArea.yMin, m_spawnArea.yMax);
            spawnPoint = new Vector2(randomX, randomY);

            // Pengecekan keamanan terhadap Player (agar tidak spawn tepat di depan mata)
            bool farFromPlayer = true;
            if (m_playerTransform != null)
            {
                farFromPlayer = Vector2.Distance(spawnPoint, m_playerTransform.position) > m_safeDistance;
            }

            Collider2D otherAsteroid = Physics2D.OverlapCircle(spawnPoint, asteroidSafetyRadius);
            bool farFromOthers = (otherAsteroid == null);

            if (farFromPlayer && farFromOthers) isSafe = true;
            safetyCheckAttempts++;
        }

        // Eksekusi Spawn
        int index = Random.Range(0, m_bigAsteroidPrefabs.Count);
        GameObject newAsteroid = Instantiate(m_bigAsteroidPrefabs[index], spawnPoint, Quaternion.Euler(0, 0, Random.Range(0, 360)));

        // Sinkronisasi data ke asteroid baru
        AsteroidController script = newAsteroid.GetComponent<AsteroidController>();
        if (script != null) script.SetBounds(m_spawnArea);
    }

    private void OnRetry()
    {
        GameObject[] asteroids = GameObject.FindGameObjectsWithTag("Asteroid");
        foreach (GameObject asteroid in asteroids) Destroy(asteroid);
        Debug.Log("AsteroidManager: Layar dibersihkan untuk memulai sesi baru.");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(m_spawnArea.center, m_spawnArea.size);
    }
}
}