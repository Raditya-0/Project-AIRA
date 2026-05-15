using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter{

// Enum pemilik peluru
public enum BulletOwner { Player, Companion }

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

    // Pemilik peluru ini
    public BulletOwner Owner { get; private set; }

    // Set pemilik peluru
    public void SetOwner(BulletOwner owner) => Owner = owner;

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
        // Hindari friendly fire dan sesama peluru
        if (collision.CompareTag("Bullet")) return;
        if (Owner == BulletOwner.Player    && collision.CompareTag("Player"))    return;
        if (Owner == BulletOwner.Player    && collision.CompareTag("Companion")) return;
        if (Owner == BulletOwner.Companion && collision.CompareTag("Companion")) return;
        if (Owner == BulletOwner.Companion && collision.CompareTag("Player"))    return;

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