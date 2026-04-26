using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter{

public class Bullet : MonoBehaviour
{
    [SerializeField]
    private float m_forwardSpeed;

    [SerializeField]
    private float m_maximumLifetime;

    [Header("VFX Settings")]
    [SerializeField]
    private GameObject m_hitVFXPrefab; // Masukkan prefab efek ledakan kecil di sini

    private float m_currentLifetime;

    void Update()
    {
        transform.position += (transform.up * m_forwardSpeed) * Time.deltaTime;

        m_currentLifetime += Time.deltaTime;

        if (m_currentLifetime >= m_maximumLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Hindari tabrakan dengan Player atau sesama peluru
        if (collision.CompareTag("Player") || collision.CompareTag("Bullet")) return;

        if (m_hitVFXPrefab != null)
        {
            // Pastikan muncul tepat di posisi peluru saat ini
            Instantiate(m_hitVFXPrefab, transform.position, Quaternion.identity);
        }

        // Hancurkan peluru terakhir agar posisi Instantiate tidak bergeser
        Destroy(gameObject);
    }
}
}