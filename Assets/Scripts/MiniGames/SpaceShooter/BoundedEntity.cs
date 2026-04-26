using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter{

public class BoundedEntity : MonoBehaviour
{
    protected Rigidbody2D m_rigidbody;

    [Header("Base Settings")]
    [SerializeField]
    protected Rect m_bounds;

    [Header("Health Settings")]
    [SerializeField]
    protected float m_maxHealth = 100f;
    protected float m_currentHealth;

    protected virtual void Awake()
    {
        m_rigidbody = GetComponent<Rigidbody2D>();
        m_currentHealth = m_maxHealth;

        // Menghitung batas layar secara otomatis berdasarkan kamera utama
        float height = Camera.main.orthographicSize * 2;
        float width = height * Camera.main.aspect;

        // Memberikan sedikit offset (misal +1) agar objek tidak langsung teleport saat menyentuh pinggir
        m_bounds = new Rect(-width / 2 - 1f, -height / 2 - 1f, width + 2f, height + 2f);
    }

    // Fungsi OnDisable ditambahkan agar bisa di-override oleh ShipController
    protected virtual void OnDisable()
    {
        // Logika default saat objek nonaktif
    }

    // Fungsi untuk menerima damage
    public virtual void TakeDamage(float amount)
    {
        m_currentHealth -= amount;

        if (m_currentHealth <= 0)
        {
            OnDie(); // Menggunakan OnDie agar sinkron dengan override di kelas anak
        }
    }

    // Diubah dari Die() menjadi OnDie() dan dibuat virtual
    protected virtual void OnDie()
    {
        // Logika default: hancurkan objek
        Destroy(gameObject);
    }

    protected virtual void LateUpdate()
    {
        HandleScreenWrap();
    }

    private void HandleScreenWrap()
    {
        float offset = 1.5f; // Sesuaikan dengan ukuran asteroid agar tidak "pop-in"
        Vector2 position = transform.position;

        if (position.x < m_bounds.xMin - offset) position.x = m_bounds.xMax + offset;
        else if (position.x > m_bounds.xMax + offset) position.x = m_bounds.xMin - offset;

        if (position.y < m_bounds.yMin - offset) position.y = m_bounds.yMax + offset;
        else if (position.y > m_bounds.yMax + offset) position.y = m_bounds.yMin - offset;

        m_rigidbody.position = position;
    }

    public void SetBounds(Rect newBounds)
    {
        m_bounds = newBounds;
    }
}
}