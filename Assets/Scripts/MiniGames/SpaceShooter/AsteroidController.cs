using UnityEngine;
using System.Collections.Generic;
using UnityEngine.VFX;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class AsteroidController : BoundedEntity
{
    [Header("Drop Settings")]
    [SerializeField] private GameObject m_collectiblePrefab;
    [Range(0, 100)]
    [SerializeField] private float m_dropChance = 20f;

    [Header("Asteroid Physics")]
    [SerializeField] private float m_forcePower = 5f;
    [SerializeField] private float m_angularPower = 100f;

    [Header("Splitting Settings")]
    [SerializeField] private List<GameObject> m_smallerAsteroidPrefabs;
    [SerializeField] private int m_splitCount = 2;
    [SerializeField] private bool m_isBigAsteroid = false;

    [Header("Score Settings")]
    [SerializeField] private int m_scoreValue = 1;

    [Header("VFX Settings")]
    [SerializeField] private GameObject m_explosionVFXPrefab;

    private void Start()
    {
        if (m_rigidbody == null) m_rigidbody = GetComponent<Rigidbody2D>();

        // 1. Arah yang benar-benar random (360 derajat)
        float randomAngle = Random.Range(0f, 360f);
        Vector2 direction = new Vector2(Mathf.Cos(randomAngle * Mathf.Deg2Rad), Mathf.Sin(randomAngle * Mathf.Deg2Rad));

        // 2. Memberikan kecepatan awal konstan sesuai forcePower
        // Mengalikan dengan mass penting karena massamu besar (20-100)
        m_rigidbody.linearVelocity = direction * m_forcePower;

        // Rotasi random
        m_rigidbody.angularVelocity = Random.Range(-m_angularPower, m_angularPower);
    }

    private void FixedUpdate()
    {
        // 3. MEMASTIKAN KECEPATAN KONSTAN
        // Mencegah asteroid melambat karena gesekan fisik Unity
        if (m_rigidbody.linearVelocity.magnitude > 0)
        {
            m_rigidbody.linearVelocity = m_rigidbody.linearVelocity.normalized * m_forcePower;
        }
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null || collision.gameObject == null) return;

        if (collision.gameObject.CompareTag("Bullet"))
        {
            Destroy(collision.gameObject);
            TakeDamage(1f); // Menghancurkan asteroid sesuai health sistem
        }
        else if (collision.gameObject.CompareTag("Player"))
        {
            CheckAndDamagePlayer(collision.gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            CheckAndDamagePlayer(collision.gameObject);
    }

    private void CheckAndDamagePlayer(GameObject playerObj)
    {
        ShipController ship = playerObj.GetComponent<ShipController>();
        // Hapus pemanggilan HandlePlayerCollision() tanpa parameter, ganti jadi:
        if (ship != null && !ship.IsSafe())
        {
            HandlePlayerCollision(ship);
        }
    }

    private void HandlePlayerCollision(ShipController ship)
    {
        float damage = m_isBigAsteroid ? 30f : 20f;

        // Panggil TakeDamage di ShipController, bukan langsung ke GameEvents
        if (ship != null)
        {
            ship.TakeDamage(damage);
        }

        OnDie();
    }
    protected override void OnDie()
    {
        // Efek Ledakan
        if (m_explosionVFXPrefab != null)
        {
            GameObject fx = Instantiate(m_explosionVFXPrefab, transform.position, Quaternion.identity);
            VisualEffect vfx = fx.GetComponent<VisualEffect>();
            if (vfx != null) vfx.SendEvent("OnPlay");
        }

        if (GameEvents.Instance != null) GameEvents.Instance.AddToScore(m_scoreValue);

        TryDropItem();

        // Logika Splitting (Pecah)
        if (m_smallerAsteroidPrefabs != null && m_smallerAsteroidPrefabs.Count > 0)
        {
            for (int i = 0; i < m_splitCount; i++)
            {
                Vector2 spreadDir = new Vector2(Mathf.Cos((360f / m_splitCount) * i * Mathf.Deg2Rad), Mathf.Sin((360f / m_splitCount) * i * Mathf.Deg2Rad));

                GameObject piece = Instantiate(m_smallerAsteroidPrefabs[Random.Range(0, m_smallerAsteroidPrefabs.Count)], transform.position + (Vector3)(spreadDir * 1.2f), Quaternion.Euler(0, 0, Random.Range(0, 360)));

                AsteroidController script = piece.GetComponent<AsteroidController>();
                if (script != null) script.SetBounds(m_bounds); // Sinkronisasi Bounds

                Rigidbody2D rbPiece = piece.GetComponent<Rigidbody2D>();
                if (rbPiece != null)
                {
                    // Berikan gaya menyebar berdasarkan m_forcePower prefab kecil tersebut
                    float pieceSpeed = script != null ? script.m_forcePower : 7f;
                    rbPiece.linearVelocity = spreadDir * pieceSpeed;
                }
            }
        }
        base.OnDie();
    }

    private void TryDropItem()
    {
        if (m_collectiblePrefab != null && Random.Range(0f, 100f) <= m_dropChance)
            Instantiate(m_collectiblePrefab, transform.position, Quaternion.identity);
    }

    public bool IsBigAsteroid() => m_isBigAsteroid;
}
}