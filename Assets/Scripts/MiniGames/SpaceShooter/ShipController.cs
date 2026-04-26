using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.VFX;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class ShipController : BoundedEntity
{
    private float m_turnInput;
    private float m_forwardInput;
    private float m_fireTimer;
    private bool m_isFiring;
    private bool m_isDead = false;

    [Header("Input Actions")]
    [SerializeField] private InputAction m_moveAction;
    [SerializeField] private InputAction m_attackAction;
    [SerializeField] private InputAction m_boostAction;

    [Header("Movement Settings")]
    [SerializeField] private float m_maxSpeed = 10f;
    [SerializeField] private float m_turnSpeed = 1.5f;
    [SerializeField] private float m_stoppingPower = 2f;
    [SerializeField] private float m_moveSpeed = 15f;

    [Header("Boost Settings")]
    [SerializeField] private float m_boostForce = 20f;

    [Header("Weapon Settings")]
    [SerializeField] private GameObject m_bulletPrefab;
    [SerializeField] private float m_fireDelay = 0.2f;
    [SerializeField] private float m_bulletSpawnOffset = 2.5f;

    [Header("Respawn & Invincibility")]
    [SerializeField] private float m_invincibilityDuration = 3f;
    private bool m_isInvincible = false;
    private SpriteRenderer m_spriteRenderer;
    private Collider2D m_collider;

    [Header("VFX Settings")]
    [SerializeField] private GameObject m_deathVFXPrefab;
    [SerializeField] private VisualEffect[] m_thrustVFXs;

    [Header("Sound Settings")]
    [SerializeField] private SoundEffectHandler m_fireSoundHandler;
    [SerializeField] private SoundEffectHandler m_hitSoundHandler;
    [SerializeField] private SoundEffectHandler m_dieSoundHandler;
    [SerializeField] private SoundEffectHandler m_thrusterSoundHandler;
    [SerializeField] private SoundEffectHandler m_boosterSoundHandler;
    [SerializeField] private SoundEffectHandler m_collectSoundHandler;

    [Header("Manager Reference")]
    [SerializeField] private ScoreManager m_scoreManager;

    private bool m_isThrusterPlaying = false;
    private bool m_isBoosterPlaying = false;

    public bool IsSafe() => m_isInvincible || m_isDead;

    protected override void Awake()
    {
        base.Awake();
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_collider = GetComponent<Collider2D>();
        m_rigidbody.gravityScale = 0;
        m_rigidbody.linearDamping = m_stoppingPower;
        m_fireTimer = m_fireDelay;
    }

    private void OnEnable()
    {
        m_moveAction.Enable();
        m_attackAction.Enable();
        m_boostAction.Enable();
        if (GameEvents.Instance != null)
        {
            GameEvents.Instance.onPlayerDeath += ExecuteDeathEvent;
            GameEvents.Instance.onRetry += OnRetry;
        }
    }

    protected override void OnDisable()
    {
        m_moveAction.Disable();
        m_attackAction.Disable();
        m_boostAction.Disable();
        if (GameEvents.Instance != null)
        {
            GameEvents.Instance.onPlayerDeath -= ExecuteDeathEvent;
            GameEvents.Instance.onRetry -= OnRetry;
        }
        base.OnDisable();
    }

    private void Update()
    {
        if (m_isDead) { HandleVisualEffects(); return; }

        Vector2 moveInput = m_moveAction.ReadValue<Vector2>();
        m_turnInput = moveInput.x;
        m_forwardInput = moveInput.y;
        m_isFiring = m_attackAction.IsPressed();

        if (m_boostAction.IsPressed())
            m_rigidbody.AddForce(transform.up * m_boostForce * Time.deltaTime, ForceMode2D.Force);

        m_fireTimer += Time.deltaTime;
        if (m_isFiring) TrySpawnBullet();

        HandleVisualEffects();
    }

    private void HandleVisualEffects()
    {
        if (m_thrustVFXs == null) return;

        bool isThrusting = !m_isDead && m_forwardInput > 0;
        bool isBoosting = !m_isDead && m_boostAction.IsPressed();
        bool isInputActive = isThrusting || isBoosting;

        // --- SOUND THRUSTER ---
        if (isThrusting && !m_isThrusterPlaying)
        {
            m_isThrusterPlaying = true;
            if (m_thrusterSoundHandler != null) m_thrusterSoundHandler.Play();
        }
        else if (!isThrusting && m_isThrusterPlaying)
        {
            m_isThrusterPlaying = false;
            if (m_thrusterSoundHandler != null) m_thrusterSoundHandler.StopWithFade();
        }

        // --- SOUND BOOSTER ---
        if (isBoosting && !m_isBoosterPlaying)
        {
            m_isBoosterPlaying = true;
            if (m_boosterSoundHandler != null) m_boosterSoundHandler.Play();
        }
        else if (!isBoosting && m_isBoosterPlaying)
        {
            m_isBoosterPlaying = false;
            if (m_boosterSoundHandler != null) m_boosterSoundHandler.StopWithFade();
        }

        foreach (var vfx in m_thrustVFXs)
        {
            if (vfx != null)
            {
                vfx.SetBool("isThrusting", isInputActive);
                vfx.SetBool("isBoosting", isBoosting);
            }
        }
    }

    private void FixedUpdate()
    {
        if (m_isDead) return;
        m_rigidbody.MoveRotation(m_rigidbody.rotation - (m_turnInput * m_turnSpeed * 100f * Time.fixedDeltaTime));
        if (m_forwardInput != 0) m_rigidbody.AddRelativeForce(Vector2.up * m_forwardInput * m_moveSpeed);
        if (m_rigidbody.linearVelocity.magnitude > m_maxSpeed)
            m_rigidbody.linearVelocity = m_rigidbody.linearVelocity.normalized * m_maxSpeed;
    }

    private void TrySpawnBullet()
    {
        if (m_fireTimer >= m_fireDelay && m_bulletPrefab != null)
        {
            m_fireTimer = 0f;
            if (m_fireSoundHandler != null) m_fireSoundHandler.Play();

            GameObject bullet = Instantiate(m_bulletPrefab, transform.position + (transform.up * m_bulletSpawnOffset), Quaternion.identity);
            bullet.transform.up = transform.up;
            Physics2D.IgnoreCollision(bullet.GetComponent<Collider2D>(), m_collider);
        }
    }

    // LOGIKA POINT 3: Hanya putar suara DIE di sini
    protected override void OnDie()
    {
        Debug.Log("[SHIP] KAPAL MATI - Menjalankan OnDie");

        // Pastikan suara mati diputar
        if (m_dieSoundHandler != null)
        {
            Debug.Log("[SHIP] Memutar Suara DIE");
            m_dieSoundHandler.Play();
        }

        m_isDead = true;

        // Matikan suara mesin seketika agar tidak menumpuk dengan suara ledakan
        if (m_thrusterSoundHandler != null) m_thrusterSoundHandler.StopWithFade();
        if (m_boosterSoundHandler != null) m_boosterSoundHandler.StopWithFade();

        if (m_deathVFXPrefab != null)
        {
            Instantiate(m_deathVFXPrefab, transform.position, Quaternion.identity);
        }

        m_spriteRenderer.enabled = false;
        m_collider.enabled = false;
        m_rigidbody.simulated = false;

        // Hanya jalankan Respawn jika game belum Over
        if (m_scoreManager != null && m_scoreManager.GetCurrentLives() > 0)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    private void ExecuteDeathEvent() { if (!m_isDead) OnDie(); }

    private IEnumerator RespawnRoutine()
    {
        m_isInvincible = true;
        yield return new WaitForSeconds(1f);
        m_isDead = false;
        m_rigidbody.simulated = true;
        m_rigidbody.linearVelocity = Vector2.zero;
        float timer = 0;
        while (timer < m_invincibilityDuration)
        {
            m_spriteRenderer.enabled = !m_spriteRenderer.enabled;
            yield return new WaitForSeconds(0.1f);
            timer += 0.1f;
        }
        m_spriteRenderer.enabled = true;
        m_collider.enabled = true;
        m_isInvincible = false;
    }

    private void OnRetry()
    {
        StopAllCoroutines();
        transform.position = Vector3.zero;
        m_isDead = m_isInvincible = false;
        m_spriteRenderer.enabled = m_collider.enabled = m_rigidbody.simulated = true;
        m_rigidbody.linearVelocity = Vector2.zero;
    }

    public override void TakeDamage(float amount)
    {
        if (IsSafe()) return;

        if (m_scoreManager != null)
        {
            float currentHealth = m_scoreManager.GetCurrentHealth();
            Debug.Log($"[SHIP] Kena Hit. Darah: {currentHealth}, Damage: {amount}");

            if (currentHealth > amount)
            {
                if (m_hitSoundHandler != null)
                {
                    Debug.Log("[SHIP] Memutar Suara HIT");
                    m_hitSoundHandler.Play();
                }
                else
                {
                    Debug.LogError("[SHIP] Slot Hit Sound Handler KOSONG!");
                }
            }
        }
        else
        {
            Debug.LogError("[SHIP] Score Manager belum dimasukkan ke Inspector!");
        }

        if (GameEvents.Instance != null)
            GameEvents.Instance.PlayerDamage(amount);
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Collectible"))
        {
            if (m_collectSoundHandler != null) m_collectSoundHandler.Play();

            // Logic collectible diproses di CollectibleController.cs melalui trigger tag Player
            // Objek dihancurkan di sana untuk mencegah double trigger
        }
    }
}
}