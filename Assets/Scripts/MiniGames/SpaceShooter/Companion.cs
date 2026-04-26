using UnityEngine;
using UnityEngine.VFX;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class CompanionController : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform m_playerTransform;
    [SerializeField] private float m_followRadius = 4f;
    [SerializeField] private float m_idleRadius = 2f;
    [SerializeField] private float m_boostRadius = 10f; // RADIUS BARU: Kapan companion harus nge-boost
    [SerializeField] private float m_smoothTime = 0.4f;
    private Vector3 m_currentVelocity;

    [Header("Combat Settings")]
    [SerializeField] private GameObject m_bulletPrefab;
    [SerializeField] private float m_fireDelay = 0.5f;
    [SerializeField] private float m_detectionRadius = 8f;
    [SerializeField] private float m_bulletSpeed = 15f; 

    private float m_fireTimer;
    private GameObject m_currentTarget; 

    [Header("VFX Settings")]
    [SerializeField] private VisualEffect m_leftThrusterVFX;
    [SerializeField] private VisualEffect m_rightThrusterVFX;

    [Header("Sound Settings")]
    public SoundEffectHandler fireSoundHandler;

    private void Update()
    {
        if (m_playerTransform == null || IsPlayerDisabled())
        {
            ToggleThrusters(false);
            m_currentVelocity = Vector3.zero;
            return;
        }

        HandleCombat();
        HandleMovement();
    }

    private void HandleMovement()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, m_playerTransform.position);

        // 1. KONDISI BOOST (Jarak sangat jauh)
        if (distanceToPlayer > m_boostRadius)
        {
            // Bergerak lebih cepat dengan membagi smoothTime menjadi setengahnya
            transform.position = Vector3.SmoothDamp(transform.position, m_playerTransform.position, ref m_currentVelocity, m_smoothTime / 2f);
            ToggleThrusters(true);
        }
        // 2. KONDISI NORMAL (Jarak lumayan jauh)
        else if (distanceToPlayer > m_followRadius)
        {
            transform.position = Vector3.SmoothDamp(transform.position, m_playerTransform.position, ref m_currentVelocity, m_smoothTime);
            ToggleThrusters(true);
        }
        // 3. KONDISI IDLE (Jarak sudah dekat)
        else if (distanceToPlayer < m_idleRadius)
        {
            m_currentVelocity = Vector3.Lerp(m_currentVelocity, Vector3.zero, Time.deltaTime * 5f);
            ToggleThrusters(false);
        }
        
        transform.position += m_currentVelocity * Time.deltaTime;

        // ROTASI PINTAR
        if (m_currentTarget != null)
        {
            Vector2 directionToTarget = (m_currentTarget.transform.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, directionToTarget);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
        else
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, m_playerTransform.rotation, Time.deltaTime * 3f);
        }
    }

    private void HandleCombat()
    {
        m_fireTimer += Time.deltaTime;

        if (m_currentTarget != null)
        {
            float distanceToTarget = Vector2.Distance(transform.position, m_currentTarget.transform.position);
            if (!m_currentTarget.activeInHierarchy || distanceToTarget > m_detectionRadius)
            {
                m_currentTarget = null; 
            }
        }

        if (m_currentTarget == null)
        {
            m_currentTarget = FindBestTarget();
        }

        if (m_currentTarget != null && m_fireTimer >= m_fireDelay)
        {
            Shoot(m_currentTarget.transform.position);
            m_fireTimer = 0f;
        }
    }

    private GameObject FindBestTarget()
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, m_detectionRadius);
        GameObject bestTarget = null;

        int highestPriority = -1;
        float closestDistance = Mathf.Infinity;

        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Asteroid"))
            {
                int priority = GetAsteroidPriority(hit.gameObject.name);
                float distance = Vector2.Distance(transform.position, hit.transform.position);

                if (priority > highestPriority || (priority == highestPriority && distance < closestDistance))
                {
                    highestPriority = priority;
                    closestDistance = distance;
                    bestTarget = hit.gameObject;
                }
            }
        }
        return bestTarget;
    }

    private int GetAsteroidPriority(string asteroidName)
    {
        string nameLower = asteroidName.ToLower();

        if (nameLower.Contains("large")) return 3; 
        if (nameLower.Contains("medium")) return 2;
        if (nameLower.Contains("small")) return 1;

        return 0; 
    }

    private void Shoot(Vector3 targetPos)
    {
        if (m_bulletPrefab == null) return;

        Vector2 direction = (targetPos - transform.position).normalized;
        Vector3 spawnPosition = transform.position + (Vector3)direction * 0.8f;

        GameObject bullet = Instantiate(m_bulletPrefab, spawnPosition, Quaternion.identity);
        bullet.transform.up = direction;

        if (fireSoundHandler != null)
        {
            fireSoundHandler.Play(); 
        }
    }

    private void ToggleThrusters(bool state)
    {
        if (m_leftThrusterVFX != null) m_leftThrusterVFX.SetBool("isThrusting", state);
        if (m_rightThrusterVFX != null) m_rightThrusterVFX.SetBool("isThrusting", state);
    }

    private bool IsPlayerDisabled()
    {
        ShipController ship = m_playerTransform.GetComponent<ShipController>();
        return ship != null && ship.IsSafe();
    }

    private void OnDrawGizmosSelected()
    {
        // Menggambar Follow Radius (Hijau)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, m_followRadius);

        // Menggambar Idle Radius (Kuning)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, m_idleRadius);

        // Menggambar Boost Radius (Biru)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, m_boostRadius);

        // Menggambar Detection Radius (Merah)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, m_detectionRadius);
    }
}
}