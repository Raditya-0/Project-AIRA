using UnityEngine;
using System.Collections;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class Collectible : MonoBehaviour
{
    [Header("Bonuses")]
    [SerializeField] private float m_healthBonus = 10f;
    [SerializeField] private int m_scoreBonus = 10;

    [Header("Despawn Settings")]
    [SerializeField] private float m_lifeTime = 10f;       // Total waktu item di scene
    [SerializeField] private float m_blinkStartAt = 3f;   // Mulai kedip saat sisa 3 detik
    [SerializeField] private float m_blinkSpeed = 0.1f;    // Kecepatan kedip

    private SpriteRenderer m_spriteRenderer;
    private bool m_isCollected = false;

    private void Awake()
    {
        m_spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // Mulai hitung mundur despawn saat item muncul
        StartCoroutine(DespawnRoutine());
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (m_isCollected) return;

        if (collision.CompareTag("Player"))
        {
            m_isCollected = true;
            
            GameEvents.Instance.PlayerHeal(m_healthBonus);
            GameEvents.Instance.AddToScore(m_scoreBonus);

            Debug.Log($"Collectible: +{m_healthBonus} Health & +{m_scoreBonus} Score!");

            // Hancurkan objek segera saat diambil
            Destroy(gameObject);
        }
    }

    private IEnumerator DespawnRoutine()
    {
        // 1. Tunggu sampai waktu mulai kedip tiba
        yield return new WaitForSeconds(m_lifeTime - m_blinkStartAt);

        float timer = m_blinkStartAt;

        // 2. Logika kedip-kedip (Blinking)
        while (timer > 0)
        {
            if (m_isCollected) yield break; // Berhenti jika sudah diambil player

            // Toggle visibility sprite
            if (m_spriteRenderer != null)
                m_spriteRenderer.enabled = !m_spriteRenderer.enabled;

            // Semakin dekat ke 0, semakin cepat kedipnya (opsional)
            float currentBlinkSpeed = (timer < 1f) ? m_blinkSpeed / 2f : m_blinkSpeed;

            yield return new WaitForSeconds(currentBlinkSpeed);
            timer -= currentBlinkSpeed;
        }

        // 3. Hilang (Despawn)
        if (!m_isCollected)
        {
            Destroy(gameObject);
        }
    }
}
}