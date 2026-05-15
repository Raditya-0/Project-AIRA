using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AIRA.MiniGames.SpaceShooter
{
    public class ShipController : ShipBase
    {
        private float m_turnInput;
        private float m_forwardInput;
        private bool  m_isFiring;
        private bool  m_isThrusterPlaying;

        [Header("Input Actions")]
        [SerializeField] private InputAction m_moveAction;
        [SerializeField] private InputAction m_attackAction;
        [SerializeField] private InputAction m_boostAction;

        [Header("Movement Settings")]
        [SerializeField] private float m_stoppingPower = 2f;
        [SerializeField] private float m_moveSpeed     = 15f;

        [Header("VFX Settings")]
        [SerializeField] private GameObject m_deathVFXPrefab;

        [Header("Sound Settings")]
        [SerializeField] private SoundEffectHandler m_hitSoundHandler;
        [SerializeField] private SoundEffectHandler m_dieSoundHandler;
        [SerializeField] private SoundEffectHandler m_collectSoundHandler;

        [Header("Manager Reference")]
        [SerializeField] private ScoreManager m_scoreManager;

        // Getter status mati
        public bool IsDead() => m_isDead;

        // Pemilik peluru player
        protected override BulletOwner GetBulletOwner() => BulletOwner.Player;

        // Inisialisasi fisika player
        protected override void Awake()
        {
            base.Awake();
            m_rigidbody.gravityScale  = 0;
            m_rigidbody.linearDamping = m_stoppingPower;
            m_fireTimer               = m_fireDelay;
        }

        // Daftar input dan event
        private void OnEnable()
        {
            m_moveAction.Enable();
            m_attackAction.Enable();
            m_boostAction.Enable();
            if (GameEvents.Instance == null) return;
            GameEvents.Instance.onPlayerDeath += ExecuteDeathEvent;
            GameEvents.Instance.onRetry       += OnRetry;
        }

        // Hapus input dan event
        protected override void OnDisable()
        {
            m_moveAction.Disable();
            m_attackAction.Disable();
            m_boostAction.Disable();
            if (GameEvents.Instance != null)
            {
                GameEvents.Instance.onPlayerDeath -= ExecuteDeathEvent;
                GameEvents.Instance.onRetry       -= OnRetry;
            }
            base.OnDisable();
        }

        // Loop input dan tembak
        private void Update()
        {
            if (Time.timeScale == 0f) return;
            if (m_isDead) { HandleVisualEffects(); return; }

            Vector2 moveInput = m_moveAction.ReadValue<Vector2>();
            m_turnInput    = moveInput.x;
            m_forwardInput = moveInput.y;
            m_isFiring     = m_attackAction.IsPressed();

            if (m_boostAction.IsPressed()) TryBoost();
            else                           StopBoost();

            TickEnergyRegen();
            m_fireTimer += Time.deltaTime;
            if (m_isFiring) TrySpawnBullet();

            HandleVisualEffects();
        }

        // Coba tembak berdasar timer
        private void TrySpawnBullet()
        {
            if (m_fireTimer < m_fireDelay) return;
            m_fireTimer = 0f;
            Shoot(Vector3.zero);
        }

        // Suara dan VFX thruster
        private void HandleVisualEffects()
        {
            if (m_thrustVFXs == null) return;

            bool isThrusting = !m_isDead && m_forwardInput > 0;
            bool isBoosting  = !m_isDead && m_isBoosting;

            if (isThrusting && !m_isThrusterPlaying)
            {
                m_isThrusterPlaying = true;
                m_thrusterSoundHandler?.Play();
            }
            else if (!isThrusting && m_isThrusterPlaying)
            {
                m_isThrusterPlaying = false;
                m_thrusterSoundHandler?.StopWithFade();
            }

            foreach (var vfx in m_thrustVFXs)
            {
                if (vfx == null) continue;
                vfx.SetBool("isThrusting", isThrusting || isBoosting);
                vfx.SetBool("isBoosting", isBoosting);
            }
        }

        // Fisika gerak dan rotasi
        private void FixedUpdate()
        {
            if (m_isDead) return;
            m_rigidbody.MoveRotation(m_rigidbody.rotation - m_turnInput * m_turnSpeed * 100f * Time.fixedDeltaTime);
            if (m_forwardInput != 0) m_rigidbody.AddRelativeForce(Vector2.up * m_forwardInput * m_moveSpeed);
            if (m_rigidbody.linearVelocity.magnitude > m_maxSpeed)
                m_rigidbody.linearVelocity = m_rigidbody.linearVelocity.normalized * m_maxSpeed;
        }

        // Mati: VFX, nonaktif, respawn
        protected override void OnDie()
        {
            Debug.Log("[SHIP] KAPAL MATI - Menjalankan OnDie");
            m_dieSoundHandler?.Play();

            m_isDead = true;
            m_thrusterSoundHandler?.StopWithFade();
            m_boosterSoundHandler?.StopWithFade();

            if (m_deathVFXPrefab != null)
                Instantiate(m_deathVFXPrefab, transform.position, Quaternion.identity);

            m_spriteRenderer.enabled = false;
            m_collider.enabled       = false;
            m_rigidbody.simulated    = false;

            if (m_scoreManager != null && m_scoreManager.GetCurrentLives() > 0)
                StartCoroutine(RespawnRoutine());
        }

        // Trigger death dari event
        private void ExecuteDeathEvent() { if (!m_isDead) OnDie(); }

        // Blink dan respawn player
        private IEnumerator RespawnRoutine()
        {
            m_isInvincible = true;
            yield return new WaitForSeconds(1f);

            m_isDead                   = false;
            m_rigidbody.simulated      = true;
            m_rigidbody.linearVelocity = Vector2.zero;

            float timer = 0f;
            while (timer < m_invincibilityDuration)
            {
                m_spriteRenderer.enabled = !m_spriteRenderer.enabled;
                yield return new WaitForSeconds(0.1f);
                timer += 0.1f;
            }

            m_spriteRenderer.enabled = true;
            m_collider.enabled       = true;
            GameEvents.Instance.PlayerRespawnEnd();
            m_isInvincible = false;
        }

        // Reset state saat retry
        private void OnRetry()
        {
            StopAllCoroutines();
            transform.position         = Vector3.zero;
            m_isDead = m_isInvincible  = false;
            m_spriteRenderer.enabled   = m_collider.enabled = m_rigidbody.simulated = true;
            m_rigidbody.linearVelocity = Vector2.zero;
        }

        // Terima damage dari tabrakan
        public override void TakeDamage(float amount)
        {
            if (IsSafe()) return;
            float currentHealth = HealthManager.Instance != null ? HealthManager.Instance.GetCurrentHealth() : 0f;
            Debug.Log($"[SHIP] Kena Hit. Darah: {currentHealth}, Damage: {amount}");
            if (currentHealth > amount) m_hitSoundHandler?.Play();
            GameEvents.Instance?.PlayerDamage(amount);
        }

        // Pickup collectible player
        public void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Collectible"))
                m_collectSoundHandler?.Play();
        }
    }
}
