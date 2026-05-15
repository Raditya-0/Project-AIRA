using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class AsteroidManager : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private List<GameObject> m_bigAsteroidPrefabs;
    [SerializeField] private Camera m_camera;
    [SerializeField] private float  m_spawnMargin      = 1.5f;
    [SerializeField] private float  m_bigAsteroidSpeed = 2f;

    [Header("Dynamic Difficulty")]
    [SerializeField] private int   m_baseMinAsteroids    = 3;
    [SerializeField] private int   m_maxAsteroidsAllowed = 15;
    [SerializeField] private int   m_scorePerDifficulty  = 100;
    [SerializeField] private float m_minSpawnInterval    = 0.2f;
    [SerializeField] private float m_baseSpawnInterval   = 1f;

    [Header("Safety")]
    [SerializeField] private Transform m_playerTransform;

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

    // Kelola populasi asteroid dinamis
    private IEnumerator ManageAsteroidPopulation()
    {
        while (true)
        {
            AsteroidController[] all = Object.FindObjectsByType<AsteroidController>(
                FindObjectsSortMode.None);

            int currentCount = 0;
            foreach (var a in all)
                if (a != null && a.IsBigAsteroid()) currentCount++;

            int targetCount = GetCurrentMinAsteroids();

            if (currentCount < targetCount)
                SpawnBigAsteroid();

            yield return new WaitForSeconds(GetCurrentSpawnInterval());
        }
    }

    // Hitung target asteroid berdasarkan skor
    private int GetCurrentMinAsteroids()
    {
        int bonus = ScoreManager.Instance != null
            ? ScoreManager.Instance.GetCurrentScore() / m_scorePerDifficulty
            : 0;
        return Mathf.Min(m_baseMinAsteroids + bonus, m_maxAsteroidsAllowed);
    }

    // Hitung interval spawn berdasarkan skor
    private float GetCurrentSpawnInterval()
    {
        int bonus = ScoreManager.Instance != null
            ? ScoreManager.Instance.GetCurrentScore() / m_scorePerDifficulty
            : 0;
        float interval = m_baseSpawnInterval - (bonus * 0.05f);
        return Mathf.Max(interval, m_minSpawnInterval);
    }

    // Titik spawn luar kamera
    private Vector2 GetSpawnPointOutsideCamera()
    {
        float camHeight = m_camera.orthographicSize;
        float camWidth  = camHeight * m_camera.aspect;

        int side = Random.Range(0, 4);
        return side switch
        {
            0 => new Vector2(Random.Range(-camWidth, camWidth),  camHeight + m_spawnMargin), // atas
            1 => new Vector2(Random.Range(-camWidth, camWidth), -camHeight - m_spawnMargin), // bawah
            2 => new Vector2( camWidth + m_spawnMargin, Random.Range(-camHeight, camHeight)), // kanan
            _ => new Vector2(-camWidth - m_spawnMargin, Random.Range(-camHeight, camHeight)), // kiri
        };
    }

    // Spawn asteroid besar baru
    private void SpawnBigAsteroid()
    {
        if (m_bigAsteroidPrefabs.Count == 0) return;
        if (m_camera == null)
        {
            Debug.LogWarning("[AsteroidManager] Camera belum di-assign.");
            return;
        }

        Vector2 spawnPoint     = Vector2.zero;
        bool    isSafe         = false;
        int     safetyAttempts = 0;
        float   safetyRadius   = 2.5f;

        while (!isSafe && safetyAttempts < 25)
        {
            spawnPoint = GetSpawnPointOutsideCamera();

            Collider2D otherAsteroid = Physics2D.OverlapCircle(spawnPoint, safetyRadius);
            if (otherAsteroid == null) isSafe = true;
            safetyAttempts++;
        }

        int        index       = Random.Range(0, m_bigAsteroidPrefabs.Count);
        GameObject newAsteroid = Instantiate(
            m_bigAsteroidPrefabs[index],
            spawnPoint,
            Quaternion.Euler(0, 0, Random.Range(0, 360)));

        AsteroidController script = newAsteroid.GetComponent<AsteroidController>();
        if (script != null)
        {
            float camHeight = m_camera.orthographicSize;
            float camWidth  = camHeight * m_camera.aspect;
            script.SetBounds(new Rect(-camWidth, -camHeight, camWidth * 2, camHeight * 2));
            script.SetSpeed(m_bigAsteroidSpeed);
        }
    }

    private void OnRetry()
    {
        GameObject[] asteroids = GameObject.FindGameObjectsWithTag("Asteroid");
        foreach (GameObject asteroid in asteroids) Destroy(asteroid);
        Debug.Log("AsteroidManager: Layar dibersihkan untuk memulai sesi baru.");
    }

    // Visualisasi batas kamera
    private void OnDrawGizmosSelected()
    {
        if (m_camera == null) return;
        float h = m_camera.orthographicSize;
        float w = h * m_camera.aspect;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(m_camera.transform.position, new Vector3(w * 2, h * 2, 0));
    }
}
}