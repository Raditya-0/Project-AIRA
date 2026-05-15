using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public class PlatformerSFX : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource _sfx;
        [SerializeField] private AudioClip   _jumpClip;
        [SerializeField] private AudioClip   _landClip;
        [SerializeField] private AudioClip   _stepClip;

        [Header("Settings")]
        [SerializeField] private float _stepInterval = 0.3f;

        [Header("Ground Detection")]
        [SerializeField] private Rigidbody2D _rb;
        [SerializeField] private LayerMask   _groundLayer;
        [SerializeField] private float       _groundCheckRadius = 0.15f;

        private float _stepTimer;
        private bool  _wasGrounded;

        // Deteksi landing tiap frame
        private void Update()
        {
            bool grounded = CheckGrounded();
            if (!_wasGrounded && grounded) PlayLand();
            _wasGrounded  = grounded;
            _stepTimer   += Time.deltaTime;
        }

        // Cek menyentuh tanah
        private bool CheckGrounded()
        {
            Vector2 checkPos = (Vector2)transform.position + Vector2.down * 0.5f;
            return Physics2D.OverlapCircle(checkPos, _groundCheckRadius, _groundLayer);
        }

        // Play suara lompat
        public void PlayJump()
        {
            if (_sfx == null || _jumpClip == null) return;
            _sfx.PlayOneShot(_jumpClip);
        }

        // Play suara mendarat
        public void PlayLand()
        {
            if (_sfx == null || _landClip == null) return;
            _sfx.PlayOneShot(_landClip);
        }

        // Play suara langkah berkala
        public void PlayStep()
        {
            if (_sfx == null || _stepClip == null) return;
            if (!CheckGrounded()) return;
            if (_rb != null && Mathf.Abs(_rb.linearVelocity.x) < 0.1f) return;
            if (_stepTimer < _stepInterval) return;
            _stepTimer = 0f;
            _sfx.PlayOneShot(_stepClip);
        }
    }
}
