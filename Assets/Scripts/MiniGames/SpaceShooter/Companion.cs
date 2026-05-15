using System;
using System.Collections;
using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter
{
    public class CompanionController : ShipBase
    {
        private enum CompanionState
        {
            Escorting, Shooting, CollectingItem,
            Dodging, Boosting, Intercepting, Bodyguard
        }

        private CompanionState m_currentState;
        private CompanionState m_previousState;

        [Header("Player Reference")]
        [SerializeField] private Transform m_playerTransform;

        [Header("Movement Settings")]
        [SerializeField] private float m_chaseSpeed    = 12f;
        [SerializeField] private float m_linearDamping = 1f;

        [Header("Escort Settings")]
        [SerializeField] private float m_escortDistance       = 3f;
        [SerializeField] private float m_boostTriggerDistance = 8f;

        [Header("Dodge Settings")]
        [SerializeField] private float m_dodgeRadius            = 3f;
        [SerializeField] private float m_dodgeForce             = 8f;
        [SerializeField] private float m_asteroidPredictionTime = 0.5f;

        [Header("Collectible Settings")]
        [SerializeField] private float m_collectibleHealthThreshold = 0.4f;
        [SerializeField] private float m_collectiblePickupRadius    = 0.5f;

        [Header("Combat Settings")]
        [SerializeField] private float m_detectionRadius = 8f;

        [Header("Fire Coordination")]
        [SerializeField] private float m_fireCoordConeAngle = 30f;
        [SerializeField] private float m_fireCoordRange     = 15f;
        [SerializeField] private float m_approachSide       = 2f;

        [Header("Cover Settings")]
        [SerializeField] private float m_coverCheckRadius   = 10f;
        [SerializeField] private float m_bodyguardDuration  = 5f;
        [SerializeField] private float m_bodyguardDistance  = 2f;

        [Header("Adaptive Aggression")]
        [SerializeField] private float m_aggressiveAsteroidThreshold = 5f;
        [SerializeField] private float m_calmAsteroidThreshold       = 2f;
        [SerializeField] private float m_aggressiveChaseSpeed        = 16f;
        [SerializeField] private float m_calmChaseSpeed              = 8f;
        [SerializeField] private float m_bodyguardChaseMultiplier    = 1.5f;

        [Header("Resource Management")]
        [SerializeField] private float m_boostMinEnergyThreshold = 0.6f;
        [SerializeField] private float m_emergencyBoostThreshold = 0.3f;

        [Header("Intent Detection")]
        [SerializeField] private float m_playerCollectibleDetectRadius = 3f;
        [SerializeField] private float m_playerBoostDetectSpeed        = 12f;
        [SerializeField] private float m_intentCheckInterval           = 0.5f;

        private float       m_intentTimer;
        private Rigidbody2D m_playerRigidbody;

        private Vector2?    m_collectibleTarget;
        private GameObject  m_currentTarget;
        private Vector3     m_lastPosition;

        private bool       m_isBodyguardMode;
        private float      m_bodyguardTimer;
        private GameObject m_playerTargetedAsteroid;
        private int        m_coverQuadrant;

        private enum AggressionLevel { Calm, Normal, Aggressive }
        private AggressionLevel m_aggressionLevel = AggressionLevel.Normal;

        public event Action onNearMiss;
        public event Action onCollectiblePickup;

        // Pemilik peluru companion
        protected override BulletOwner GetBulletOwner() => BulletOwner.Companion;

        // Inisialisasi fisika companion
        protected override void Awake()
        {
            base.Awake();
            m_rigidbody.gravityScale  = 0;
            m_rigidbody.linearDamping = m_linearDamping;
        }

        // Posisi awal companion
        private void Start()
        {
            if (m_playerTransform != null)
            {
                transform.position = m_playerTransform.position + Vector3.right * m_escortDistance;
                m_playerRigidbody  = m_playerTransform.GetComponent<Rigidbody2D>();
            }
        }

        // Daftar event game
        private void OnEnable()
        {
            if (GameEvents.Instance == null) return;
            GameEvents.Instance.onCollectibleSpawned += OnCollectibleSpawned;
            GameEvents.Instance.onPlayerHeal         += OnPlayerHeal;
            GameEvents.Instance.onPlayerDamage       += OnPlayerDamage;
            GameEvents.Instance.onAsteroidDestroyed  += OnAsteroidDestroyed;
            GameEvents.Instance.onRetry              += OnRetry;
            GameEvents.Instance.onPlayerRespawnEnd   += OnPlayerRespawnEnd;
        }

        // Hapus event saat nonaktif
        protected override void OnDisable()
        {
            if (GameEvents.Instance == null) return;
            GameEvents.Instance.onCollectibleSpawned -= OnCollectibleSpawned;
            GameEvents.Instance.onPlayerHeal         -= OnPlayerHeal;
            GameEvents.Instance.onPlayerDamage       -= OnPlayerDamage;
            GameEvents.Instance.onAsteroidDestroyed  -= OnAsteroidDestroyed;
            GameEvents.Instance.onRetry              -= OnRetry;
            GameEvents.Instance.onPlayerRespawnEnd   -= OnPlayerRespawnEnd;
            base.OnDisable();
        }

        // Loop utama AI companion
        private void Update()
        {
            if (m_isDead) return;
            m_lastPosition = transform.position;

            TickEnergyRegen();
            UpdateAggressionLevel();
            HandleCombat();
            DetectPlayerIntent();
            DecideState();

            if (m_previousState == CompanionState.Dodging && m_currentState != CompanionState.Dodging)
                onNearMiss?.Invoke();

            m_previousState = m_currentState;
            ExecuteState();
        }

        // Pilih state berdasarkan situasi
        private void DecideState()
        {
            // 0. Bodyguard override aktif
            if (m_isBodyguardMode)
            {
                m_currentState = CompanionState.Bodyguard;
                return;
            }

            // 1. Dodge dulu kalau asteroid dekat
            if (HasAsteroidNear(transform.position, m_dodgeRadius))
            {
                m_currentState = CompanionState.Dodging;
                return;
            }

            // 2. Mode agresif — langsung tembak tanpa tunggu
            if (m_aggressionLevel == AggressionLevel.Aggressive && m_currentTarget != null)
            {
                m_currentState = CompanionState.Shooting;
                return;
            }

            // 3. Mode santai — escort dan tembak dari posisi
            if (m_aggressionLevel == AggressionLevel.Calm)
            {
                m_currentState = CompanionState.Escorting;
                return;
            }

            // 5. Intercept asteroid target player
            m_playerTargetedAsteroid = DetectPlayerTarget();
            if (m_playerTargetedAsteroid != null)
            {
                bool sameTarget = m_currentTarget == m_playerTargetedAsteroid;
                if (!sameTarget)
                {
                    m_currentState = CompanionState.Intercepting;
                    return;
                }
            }

            // 6. Tembak target sendiri
            if (m_currentTarget != null)
            {
                m_currentState = CompanionState.Shooting;
                return;
            }

            // 7. Ambil collectible bila butuh health
            float playerHealthRatio = HealthManager.Instance != null
                ? HealthManager.Instance.GetCurrentHealth() / HealthManager.Instance.GetMaxHealth() : 1f;
            bool needsItem = (m_currentHealth / m_maxHealth < m_collectibleHealthThreshold)
                          || (playerHealthRatio < 0.5f);
            if (needsItem && m_collectibleTarget.HasValue)
            {
                m_currentState = CompanionState.CollectingItem;
                return;
            }

            // 8. Boost balik kalau terlalu jauh
            float distToPlayer = m_playerTransform != null
                ? Vector2.Distance(transform.position, m_playerTransform.position) : 0f;
            if (distToPlayer > m_boostTriggerDistance && CanBoostForPurpose(isEmergency: false))
            {
                m_currentState = CompanionState.Boosting;
                return;
            }

            // 9. Cover blind spot player
            m_currentState = CompanionState.Escorting;
        }

        // Jalankan handler state aktif
        private void ExecuteState()
        {
            switch (m_currentState)
            {
                case CompanionState.Dodging:        HandleDodging();        break;
                case CompanionState.Boosting:       HandleBoosting();       break;
                case CompanionState.Shooting:       HandleShooting();       break;
                case CompanionState.CollectingItem: HandleCollectingItem(); break;
                case CompanionState.Escorting:      HandleEscorting();      break;
                case CompanionState.Intercepting:   HandleIntercepting();   break;
                case CompanionState.Bodyguard:      HandleBodyguard();      break;
            }
        }

        // Cover blind spot player
        private void HandleEscorting()
        {
            if (m_playerTransform == null) return;

            Vector2 coverPos = GetCoverPosition();
            float   dist     = Vector2.Distance(transform.position, coverPos);

            // Rotate ke arah ancaman di quadrant cover
            Vector2 coverDir    = (coverPos - (Vector2)transform.position).normalized;
            float   targetAngle = Mathf.Atan2(coverDir.y, coverDir.x) * Mathf.Rad2Deg - 90f;
            m_rigidbody.MoveRotation(
                Mathf.LerpAngle(m_rigidbody.rotation, targetAngle, m_turnSpeed * Time.deltaTime));

            if (dist > 0.5f)
            {
                // Makin jauh makin agresif ngejar
                float forceMult = Mathf.Clamp(dist / m_escortDistance, 1f, 3f);
                m_rigidbody.AddForce(coverDir * m_chaseSpeed * forceMult, ForceMode2D.Force);
                ToggleThrusters(true);
            }
            else
            {
                // Ikuti velocity player agar tidak ketinggalan
                var playerRb = m_playerTransform.GetComponent<Rigidbody2D>();
                if (playerRb != null)
                    m_rigidbody.linearVelocity = Vector2.Lerp(
                        m_rigidbody.linearVelocity, playerRb.linearVelocity, 5f * Time.deltaTime);
                ToggleThrusters(m_rigidbody.linearVelocity.magnitude > 0.5f);
            }

            if (m_rigidbody.linearVelocity.magnitude > m_maxSpeed)
                m_rigidbody.linearVelocity = m_rigidbody.linearVelocity.normalized * m_maxSpeed;

            // Tembak dari escort saat mode santai
            if (m_aggressionLevel == AggressionLevel.Calm && m_currentTarget != null)
            {
                if (m_fireTimer >= m_fireDelay)
                {
                    Shoot(m_currentTarget.transform.position);
                    m_fireTimer = 0f;
                }
            }
        }

        // Dodge dengan prediksi asteroid
        private void HandleDodging()
        {
            Collider2D[] hits    = Physics2D.OverlapCircleAll(transform.position, m_dodgeRadius);
            Vector2      fleeDir = Vector2.zero;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Asteroid")) continue;
                var     rb           = hit.GetComponent<Rigidbody2D>();
                Vector2 predictedPos = (Vector2)hit.transform.position
                    + (rb != null ? rb.linearVelocity * m_asteroidPredictionTime : Vector2.zero);
                fleeDir += (Vector2)transform.position - predictedPos;
            }

            if (fleeDir == Vector2.zero)
            {
                TankMoveTo(m_playerTransform.position, m_dodgeForce);
            }
            else
            {
                Vector2 fleeTarget = (Vector2)transform.position + fleeDir.normalized * 5f;
                if (!HasAsteroidNear(fleeTarget, m_dodgeRadius * 0.5f))
                    TankMoveTo(fleeTarget, m_dodgeForce);
            }

            // Boost darurat saat dodge
            if (CanBoostForPurpose(isEmergency: true))
            {
                TryBoost();
            }

            ToggleThrusters(m_rigidbody.linearVelocity.magnitude > 0.1f);
        }

        // Intercept asteroid dari sisi berlawanan player
        private void HandleIntercepting()
        {
            if (m_playerTargetedAsteroid == null) return;

            Vector2 toAsteroid  = (Vector2)m_playerTargetedAsteroid.transform.position
                                - (Vector2)m_playerTransform.position;
            Vector2 sideOffset  = Vector2.Perpendicular(toAsteroid.normalized) * m_approachSide;
            Vector2 approachPos = (Vector2)m_playerTargetedAsteroid.transform.position + sideOffset;

            float distToAsteroid = Vector2.Distance(transform.position,
                m_playerTargetedAsteroid.transform.position);

            if (distToAsteroid > m_detectionRadius * 0.5f && CanBoostForPurpose(isEmergency: false))
                TryBoost();

            TankMoveTo(approachPos, m_chaseSpeed);
            ToggleThrusters(true);

            if (m_fireTimer >= m_fireDelay)
            {
                Shoot(m_playerTargetedAsteroid.transform.position);
                m_fireTimer = 0f;
            }
        }

        // Mode bodyguard saat player baru respawn
        private void HandleBodyguard()
        {
            if (m_playerTransform == null) return;

            m_bodyguardTimer -= Time.deltaTime;
            if (m_bodyguardTimer <= 0f)
            {
                m_isBodyguardMode = false;
                return;
            }

            // Stick sangat dekat ke player
            Vector2 guardPos = (Vector2)m_playerTransform.position
                             + (Vector2)m_playerTransform.right * m_bodyguardDistance;
            TankMoveTo(guardPos, m_chaseSpeed * m_bodyguardChaseMultiplier);
            ToggleThrusters(true);

            if (m_currentTarget != null && m_fireTimer >= m_fireDelay)
            {
                Shoot(m_currentTarget.transform.position);
                m_fireTimer = 0f;
            }
        }

        // Boost balik ke player
        private void HandleBoosting()
        {
            if (m_playerTransform == null) return;
            Vector2 toPlayer = ((Vector2)m_playerTransform.position - (Vector2)transform.position).normalized;

            // Boost dulu, TankMoveTo sebagai fallback
            if (CanBoostForPurpose(isEmergency: false))
                TryBoost();
            else
                TankMoveTo(m_playerTransform.position, m_chaseSpeed);

            ToggleThrusters(true);

            if (Vector2.Distance(transform.position, m_playerTransform.position) < m_escortDistance * 1.5f)
                m_currentState = CompanionState.Escorting;
        }

        // Kejar dan tembak asteroid
        private void HandleShooting()
        {
            if (m_currentTarget == null) return;

            float distToTarget = Vector2.Distance(transform.position, m_currentTarget.transform.position);

            // Boost kejar asteroid yang jauh
            if (distToTarget > m_detectionRadius * 0.5f && CanBoostForPurpose(isEmergency: false))
                TryBoost();

            TankMoveTo(m_currentTarget.transform.position, m_chaseSpeed);
            ToggleThrusters(true);

            if (m_fireTimer >= m_fireDelay)
            {
                Shoot(m_currentTarget.transform.position);
                m_fireTimer = 0f;
            }
        }

        // Kejar dan ambil collectible
        private void HandleCollectingItem()
        {
            if (!m_collectibleTarget.HasValue) return;
            TankMoveTo(m_collectibleTarget.Value, m_chaseSpeed);
            ToggleThrusters(true);

            if (Vector2.Distance(transform.position, m_collectibleTarget.Value) <= m_collectiblePickupRadius)
            {
                onCollectiblePickup?.Invoke();
                m_collectibleTarget = null;
            }
        }

        // Override tembak dengan cek arah
        protected override void Shoot(Vector3 targetPos)
        {
            Vector2 dir = (targetPos - transform.position).normalized;
            if (Vector2.Dot(transform.up, dir) < 0.5f) return;
            base.Shoot(targetPos);
        }

        // Update timer dan target
        private void HandleCombat()
        {
            m_fireTimer += Time.deltaTime;

            // Clear target hanya kalau tidak aktif
            if (m_currentTarget != null && !m_currentTarget.activeInHierarchy)
                m_currentTarget = null;

            // Selalu cari target terbaik tiap frame
            m_currentTarget = FindBestTarget();
        }

        // Cari target paling mengancam
        private GameObject FindBestTarget()
        {
            // Scan dari posisi player, bukan posisi Aira
            Vector2 scanCenter = m_playerTransform != null
                ? (Vector2)m_playerTransform.position
                : (Vector2)transform.position;

            Collider2D[] hits      = Physics2D.OverlapCircleAll(scanCenter, m_detectionRadius);
            GameObject   best      = null;
            float        bestScore = float.MinValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Asteroid")) continue;
                int   priority     = GetAsteroidPriority(hit.gameObject.name);
                float distToPlayer = Vector2.Distance(hit.transform.position, scanCenter);
                float distToSelf   = Vector2.Distance(transform.position, hit.transform.position);

                // Bonus kalau asteroid heading ke player
                var   rb           = hit.GetComponent<Rigidbody2D>();
                float headingBonus = 0f;
                if (rb != null && m_playerTransform != null)
                {
                    Vector2 toPlayer = (Vector2)m_playerTransform.position - (Vector2)hit.transform.position;
                    headingBonus = Mathf.Max(0f,
                        Vector2.Dot(rb.linearVelocity.normalized, toPlayer.normalized)) * 15f;
                }

                float score = (priority * 10f)
                            + (1f / Mathf.Max(0.1f, distToPlayer)) * 5f
                            - (distToSelf * 0.3f)
                            + headingBonus;
                if (score > bestScore) { bestScore = score; best = hit.gameObject; }
            }
            return best;
        }

        // Update level agresivitas berdasarkan situasi
        private void UpdateAggressionLevel()
        {
            int asteroidCount = 0;
            if (m_playerTransform != null)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(
                    m_playerTransform.position, m_detectionRadius * 1.5f);
                foreach (var hit in hits)
                    if (hit.CompareTag("Asteroid")) asteroidCount++;
            }

            if (asteroidCount >= m_aggressiveAsteroidThreshold)
                m_aggressionLevel = AggressionLevel.Aggressive;
            else if (asteroidCount <= m_calmAsteroidThreshold)
                m_aggressionLevel = AggressionLevel.Calm;
            else
                m_aggressionLevel = AggressionLevel.Normal;

            // Sesuaikan chase speed ke level agresivitas
            m_chaseSpeed = m_aggressionLevel switch
            {
                AggressionLevel.Aggressive => m_aggressiveChaseSpeed,
                AggressionLevel.Calm       => m_calmChaseSpeed,
                _                          => (m_aggressiveChaseSpeed + m_calmChaseSpeed) / 2f
            };
        }

        // Override boost — AI langsung lepas setelah satu pulse
        protected override void TryBoost()
        {
            if (!CanBoostForPurpose(false)) return;
            m_isBoostButtonHeld = true;
            base.TryBoost();
            StartCoroutine(ReleaseBoostNextFrame());
        }

        // Lepas flag boost frame berikutnya
        private IEnumerator ReleaseBoostNextFrame()
        {
            yield return new WaitForFixedUpdate();
            StopBoost();
        }

        // Deteksi intent player dan respond
        private void DetectPlayerIntent()
        {
            m_intentTimer -= Time.deltaTime;
            if (m_intentTimer > 0f || m_playerRigidbody == null) return;
            m_intentTimer = m_intentCheckInterval;

            // Intent 1: player heading ke collectible Aira
            if (m_collectibleTarget.HasValue)
            {
                Vector2 playerToCollectible = (Vector2)m_collectibleTarget.Value
                                            - (Vector2)m_playerTransform.position;
                float dot = Vector2.Dot(
                    m_playerRigidbody.linearVelocity.normalized,
                    playerToCollectible.normalized);
                if (dot > 0.7f)
                {
                    // Biarkan player ambil collectible
                    m_collectibleTarget = null;
                    return;
                }
            }

            // Intent 2: player boost → Aira ikut boost
            if (m_playerRigidbody.linearVelocity.magnitude > m_playerBoostDetectSpeed && CanBoost())
                TryBoost();

            // Intent 3: player mundur dari asteroid → Aira maju cover
            if (m_currentTarget == null) return;
            Vector2 toAsteroid = ((Vector2)m_currentTarget.transform.position
                                - (Vector2)m_playerTransform.position).normalized;
            float dotRetreat   = Vector2.Dot(m_playerRigidbody.linearVelocity.normalized, toAsteroid);

            // Dot negatif = player mundur dari asteroid
            if (dotRetreat < -0.6f && m_currentState != CompanionState.Shooting)
                m_currentState = CompanionState.Shooting;
        }

        // Cek boost berdasarkan konteks penggunaan
        private bool CanBoostForPurpose(bool isEmergency)
        {
            if (!CanBoost()) return false;
            float threshold = isEmergency ? m_emergencyBoostThreshold : m_boostMinEnergyThreshold;
            return m_currentEnergy / m_maxEnergy >= threshold;
        }

        // Deteksi asteroid yang diincar player
        private GameObject DetectPlayerTarget()
        {
            if (m_playerTransform == null) return null;

            Vector2 playerFacing = m_playerTransform.up;
            Vector2 playerPos    = m_playerTransform.position;

            Collider2D[] hits     = Physics2D.OverlapCircleAll(playerPos, m_fireCoordRange);
            GameObject   best     = null;
            float        bestScore = float.MinValue;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Asteroid")) continue;

                Vector2 dirToAsteroid = ((Vector2)hit.transform.position - playerPos).normalized;
                float   dot           = Vector2.Dot(playerFacing, dirToAsteroid);

                // Asteroid dalam cone tembak player
                if (dot < Mathf.Cos(m_fireCoordConeAngle * Mathf.Deg2Rad)) continue;

                float dist  = Vector2.Distance(playerPos, hit.transform.position);
                float score = dot * 10f - dist * 0.5f;
                if (score > bestScore) { bestScore = score; best = hit.gameObject; }
            }
            return best;
        }

        // Hitung posisi cover blind spot player
        private Vector2 GetCoverPosition()
        {
            if (m_playerTransform == null) return transform.position;

            Vector2 playerPos  = m_playerTransform.position;
            Vector2 playerBack = -(Vector2)m_playerTransform.up;
            Vector2 left       = -(Vector2)m_playerTransform.right;
            Vector2 right      =  (Vector2)m_playerTransform.right;

            float threatsBack  = CountThreatsInDirection(playerPos, playerBack);
            float threatsLeft  = CountThreatsInDirection(playerPos, left);
            float threatsRight = CountThreatsInDirection(playerPos, right);

            Vector2 coverDir = playerBack;
            if (threatsLeft  > threatsBack && threatsLeft  >= threatsRight) coverDir = left;
            if (threatsRight > threatsBack && threatsRight >  threatsLeft)  coverDir = right;

            return playerPos + coverDir * m_escortDistance;
        }

        // Hitung jumlah ancaman di arah tertentu
        private float CountThreatsInDirection(Vector2 origin, Vector2 direction)
        {
            Collider2D[] hits  = Physics2D.OverlapCircleAll(origin, m_coverCheckRadius);
            float        count = 0f;
            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Asteroid")) continue;
                Vector2 toAsteroid = ((Vector2)hit.transform.position - origin).normalized;
                float   dot        = Vector2.Dot(direction, toAsteroid);
                if (dot > 0.5f) count += 1f;
            }
            return count;
        }

        // Prioritas ukuran asteroid
        private int GetAsteroidPriority(string name)
        {
            string lower = name.ToLower();
            if (lower.Contains("large"))  return 3;
            if (lower.Contains("medium")) return 2;
            if (lower.Contains("small"))  return 1;
            return 0;
        }

        // Gerak pesawat: rotate dulu, lalu maju
        private void TankMoveTo(Vector2 targetPos, float speed)
        {
            Vector2 dir         = (targetPos - (Vector2)transform.position).normalized;
            float   targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;

            // Rotasi kiri/kanan saja
            float newAngle = Mathf.LerpAngle(m_rigidbody.rotation, targetAngle,
                                m_turnSpeed * Time.deltaTime);
            m_rigidbody.MoveRotation(newAngle);

            // Maju hanya kalau target di depan
            float dot = Vector2.Dot(transform.up, dir);

            if (dot > 0f)
            {
                // Target di depan — maju
                m_rigidbody.AddRelativeForce(Vector2.up * speed * dot, ForceMode2D.Impulse);
            }
            // Target di belakang — hanya rotate, tidak tambah force

            // Cap kecepatan maksimum
            if (m_rigidbody.linearVelocity.magnitude > m_maxSpeed)
                m_rigidbody.linearVelocity = m_rigidbody.linearVelocity.normalized * m_maxSpeed;
        }

        // Cek ada asteroid di titik
        private bool HasAsteroidNear(Vector2 point, float radius)
        {
            foreach (var hit in Physics2D.OverlapCircleAll(point, radius))
                if (hit.CompareTag("Asteroid")) return true;
            return false;
        }

        // Mati companion: nonaktif + respawn
        protected override void OnDie()
        {
            m_lastPosition = transform.position;
            m_isDead       = true;

            if (m_spriteRenderer != null) m_spriteRenderer.enabled = false;
            if (m_collider != null)       m_collider.enabled       = false;
            m_rigidbody.simulated = false;

            GameEvents.Instance?.CompanionDeath();
            StartCoroutine(RespawnRoutine());
        }

        // Blink dan respawn companion
        private IEnumerator RespawnRoutine()
        {
            m_isInvincible = true;
            yield return new WaitForSeconds(1f);

            transform.position         = m_lastPosition;
            m_isDead                   = false;
            m_rigidbody.simulated      = true;
            m_rigidbody.linearVelocity = Vector2.zero;

            float timer = 0f;
            while (timer < m_invincibilityDuration)
            {
                if (m_spriteRenderer != null)
                    m_spriteRenderer.enabled = !m_spriteRenderer.enabled;
                yield return new WaitForSeconds(0.1f);
                timer += 0.1f;
            }

            if (m_spriteRenderer != null) m_spriteRenderer.enabled = true;
            if (m_collider != null)       m_collider.enabled       = true;
            m_isInvincible = false;
            GameEvents.Instance?.CompanionRespawnEnd();
        }

        // Terima damage companion
        public override void TakeDamage(float amount)
        {
            if (IsSafe()) return;
            GameEvents.Instance?.CompanionDamage(amount);
            base.TakeDamage(amount);
        }

        // Collision damage asteroid
        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (IsSafe()) return;
            if (!collision.CompareTag("Asteroid")) return;
            collision.GetComponent<BoundedEntity>()?.TakeDamage(999f);
            TakeDamage(34f);
        }

        // Reset state saat retry
        private void OnRetry()
        {
            StopAllCoroutines();
            if (m_playerTransform != null)
                transform.position = m_playerTransform.position + Vector3.right * m_escortDistance;
            m_currentHealth = m_maxHealth;
            m_isDead = m_isInvincible = false;
            if (m_spriteRenderer != null) m_spriteRenderer.enabled = true;
            if (m_collider != null)       m_collider.enabled       = true;
            m_rigidbody.simulated      = true;
            m_rigidbody.linearVelocity = Vector2.zero;
            m_collectibleTarget        = null;
            m_currentTarget            = null;
        }

        // Simpan target collectible baru
        private void OnCollectibleSpawned(Vector3 pos)
        {
            if (!m_collectibleTarget.HasValue) m_collectibleTarget = pos;
        }

        // Reset collectible saat player heal
        private void OnPlayerHeal(float amount) { m_collectibleTarget = null; }

        // Hook damage player
        private void OnPlayerDamage(float amount) { }

        // Hook asteroid hancur
        private void OnAsteroidDestroyed(Vector3 pos) { }

        // Aktifkan bodyguard setelah player respawn
        private void OnPlayerRespawnEnd()
        {
            m_isBodyguardMode = true;
            m_bodyguardTimer  = m_bodyguardDuration;
        }

        // Gizmos debug AI
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, m_dodgeRadius);
            // Detection radius dari posisi player
            Vector3 scanCenter = m_playerTransform != null ? m_playerTransform.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(scanCenter, m_detectionRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, m_boostTriggerDistance);

            if (m_collectibleTarget.HasValue)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, (Vector3)m_collectibleTarget.Value);
            }

            // Gizmos cover position
            if (m_playerTransform != null)
            {
                Gizmos.color = Color.green;
                Vector2 coverPos = GetCoverPosition();
                Gizmos.DrawWireSphere(coverPos, 0.5f);
                Gizmos.DrawLine(transform.position, coverPos);
            }
        }
    }
}
