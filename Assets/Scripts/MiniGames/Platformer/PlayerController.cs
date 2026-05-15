using UnityEngine;
using UnityEngine.InputSystem;

namespace AIRA.MiniGames.Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _jumpForce = 10f;

        [Header("Ground Check")]
        [SerializeField] private Transform  _groundCheck;
        [SerializeField] private float      _groundCheckRadius = 0.1f;
        [SerializeField] private LayerMask  _groundLayer;
        [SerializeField] private LayerMask  _airaLayer;

        [Header("References")]
        [SerializeField] private Animator        _animator;
        [SerializeField] private SpriteRenderer  _spriteRenderer;
        [SerializeField] private PlatformerSFX   _sfx;

        // Properti akses luar
        public bool IsGrounded { get; private set; }
        public bool IsOnAira   { get; private set; }

        private Rigidbody2D _rb;
        private float       _horizontalInput;

        // Inisialisasi komponen
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        // Baca input setiap frame
        private void Update()
        {
            bool playable = GameManager.Instance?.IsMinigameActive() == true;

            CheckGrounded();

            if (playable)
            {
                _horizontalInput = Keyboard.current.dKey.isPressed ? 1f :
                                   Keyboard.current.aKey.isPressed ? -1f : 0f;

                if (Keyboard.current.spaceKey.wasPressedThisFrame && IsGrounded)
                    Jump();

                if (IsGrounded && Mathf.Abs(_horizontalInput) > 0.01f)
                    _sfx?.PlayStep();
            }
            else
            {
                _horizontalInput = 0f;
            }

            UpdateAnimator();
            FlipSprite();
        }

        // Terapkan movement physics
        private void FixedUpdate()
        {
            if (GameManager.Instance?.IsMinigameActive() != true) return;

            if (!PlatformerGame.Instance?.IsGameActive == true)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }
            _rb.linearVelocity = new Vector2(_horizontalInput * _moveSpeed, _rb.linearVelocity.y);
        }

        // Cek menyentuh tanah
        private void CheckGrounded()
        {
            Transform checkPoint = _groundCheck != null ? _groundCheck : transform;
            LayerMask combined   = _groundLayer | _airaLayer;
            IsGrounded = Physics2D.OverlapCircle(checkPoint.position, _groundCheckRadius, combined);
            IsOnAira   = Physics2D.OverlapCircle(checkPoint.position, _groundCheckRadius, _airaLayer);
        }

        // Lompat dari posisi sekarang
        private void Jump()
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpForce);
            _animator?.SetTrigger("doJump");
            _sfx?.PlayJump();
        }

        // Flip sprite sesuai arah
        private void FlipSprite()
        {
            if (_spriteRenderer == null || Mathf.Approximately(_horizontalInput, 0f)) return;
            _spriteRenderer.flipX = _horizontalInput < 0f;
        }

        // Update parameter animator
        private void UpdateAnimator()
        {
            if (_animator == null) return;
            _animator.SetBool("isRunning",  Mathf.Abs(_horizontalInput) > 0.01f);
            _animator.SetBool("isGrounded", IsGrounded);
            _animator.SetFloat("yVelocity", _rb.linearVelocity.y);
        }
    }
}
