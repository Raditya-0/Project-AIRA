using System.Collections;
using UnityEngine;
using UnityEngine.VFX;

namespace AIRA.MiniGames.SpaceShooter
{
    public abstract class ShipBase : BoundedEntity
    {
        [Header("Weapon Settings")]
        [SerializeField] protected GameObject m_bulletPrefab;
        [SerializeField] protected float m_fireDelay        = 0.2f;
        [SerializeField] protected float m_bulletSpawnOffset = 0.8f;

        [Header("Boost Settings")]
        [SerializeField] protected float m_boostForce    = 20f;
        [SerializeField] protected float m_boostDuration  = 0.3f;

        [Header("Energy Settings")]
        [SerializeField] protected float m_maxEnergy       = 100f;
        [SerializeField] protected float m_boostEnergyCost = 100f;
        [SerializeField] protected float m_energyRegenRate = 20f;

        [Header("Movement Settings")]
        [SerializeField] protected float m_maxSpeed  = 15f;
        [SerializeField] protected float m_turnSpeed = 8f;

        [Header("Respawn Settings")]
        [SerializeField] protected float m_invincibilityDuration = 3f;

        [Header("VFX Settings")]
        [SerializeField] protected VisualEffect[] m_thrustVFXs;

        [Header("Sound Settings")]
        [SerializeField] protected SoundEffectHandler m_fireSoundHandler;
        [SerializeField] protected SoundEffectHandler m_thrusterSoundHandler;
        [SerializeField] protected SoundEffectHandler m_boosterSoundHandler;

        protected float          m_fireTimer;
        protected bool           m_isDead;
        protected bool           m_isInvincible;
        protected bool           m_isBoosting;
        protected bool           m_isBoostButtonHeld;
        protected float          m_currentEnergy;
        protected SpriteRenderer m_spriteRenderer;
        protected Collider2D     m_collider;

        // Inisialisasi komponen dasar
        protected override void Awake()
        {
            base.Awake();
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_collider       = GetComponent<Collider2D>();
            m_currentEnergy  = m_maxEnergy;
        }

        // Wajib diimplementasi child
        protected abstract BulletOwner GetBulletOwner();

        // Override mati tiap child
        protected abstract override void OnDie();

        // Status kebal damage
        public virtual bool IsSafe() => m_isDead || m_isInvincible;

        // Spawn peluru ke depan
        protected virtual void Shoot(Vector3 targetPos)
        {
            if (m_bulletPrefab == null) return;
            Vector3 spawnPos    = transform.position + transform.up * m_bulletSpawnOffset;
            GameObject bullet   = Instantiate(m_bulletPrefab, spawnPos, Quaternion.identity);
            bullet.transform.up = transform.up;
            bullet.GetComponent<Bullet>()?.SetOwner(GetBulletOwner());

            var bulletCol = bullet.GetComponent<Collider2D>();
            if (bulletCol != null && m_collider != null)
                Physics2D.IgnoreCollision(bulletCol, m_collider);

            m_fireSoundHandler?.Play();
        }

        // Boost hanya saat ada energy cukup
        protected virtual void TryBoost()
        {
            if (!CanBoost()) return;
            m_isBoostButtonHeld = true;
            StartCoroutine(BoostRoutine());
        }

        // Hentikan boost saat tombol dilepas
        protected virtual void StopBoost()
        {
            m_isBoostButtonHeld = false;
        }

        // Dorong kapal ikut rotasi real-time
        private IEnumerator BoostRoutine()
        {
            m_isBoosting = true;
            m_boosterSoundHandler?.Play();

            float energyAtStart = m_currentEnergy;
            float elapsed       = 0f;

            while (elapsed < m_boostDuration && m_currentEnergy > 0f && m_isBoostButtonHeld)
            {
                elapsed        += Time.fixedDeltaTime;
                m_currentEnergy = Mathf.Max(0f, energyAtStart * (1f - elapsed / m_boostDuration));
                OnEnergyChanged();
                // Ambil transform.up tiap frame
                m_rigidbody.AddForce(transform.up * m_boostForce, ForceMode2D.Force);
                yield return new WaitForFixedUpdate();
            }

            // Nol energy hanya kalau boost selesai penuh
            if (m_isBoostButtonHeld || m_currentEnergy <= 0f)
            {
                m_currentEnergy = 0f;
                OnEnergyChanged();
            }

            m_isBoosting        = false;
            m_isBoostButtonHeld = false;
        }

        // Toggle semua VFX thruster
        protected virtual void ToggleThrusters(bool state)
        {
            if (m_thrustVFXs == null) return;
            foreach (var vfx in m_thrustVFXs)
                if (vfx != null) vfx.SetBool("isThrusting", state);
        }

        // Regenerasi energy pelan-pelan
        protected void TickEnergyRegen()
        {
            if (m_isBoosting) return;
            if (m_currentEnergy >= m_maxEnergy) return;
            m_currentEnergy = Mathf.Min(m_currentEnergy + m_energyRegenRate * Time.deltaTime, m_maxEnergy);
            OnEnergyChanged();
        }

        // Notifikasi perubahan energy
        protected virtual void OnEnergyChanged() { }

        // Getter energy untuk UI
        public float GetCurrentEnergy() => m_currentEnergy;
        public float GetMaxEnergy()     => m_maxEnergy;

        // Cek boleh boost sekarang
        public bool CanBoost() => !m_isBoosting && m_currentEnergy >= m_maxEnergy * 0.3f;
    }
}
