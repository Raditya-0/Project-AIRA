using UnityEngine;
using AIRA.Voice;

namespace AIRA.MiniGames.Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class AiraAIController : MonoBehaviour
    {
        // State machine Aira AI
        private enum AiraState
        {
            Idle, MoveToKey, WaitForStack,
            ActAsBase, MoveToDoor, Done
        }

        [Header("Movement")]
        [SerializeField] private float _moveSpeed       = 3.5f;
        [SerializeField] private float _jumpForce       = 9f;
        [SerializeField] private float _stackWaitRadius = 1.5f;

        [Header("Ground Check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private float     _groundCheckRadius = 0.1f;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Thresholds")]
        [SerializeField] private float _arriveRadius     = 0.4f;
        [SerializeField] private float _highPlatformDiff = 2.5f;

        [Header("References")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private KeyPickup        _key;
        [SerializeField] private Door             _door;
        [SerializeField] private Animator         _animator;
        [SerializeField] private SpriteRenderer   _spriteRenderer;

        private Rigidbody2D _rb;
        private AiraState   _state = AiraState.Idle;
        private bool        _isGrounded;
        private bool        _hasSpokenBase;
        private bool        _hasSpokenDoor;

        // Inisialisasi komponen
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        // Mulai AI setelah start
        private void Start()
        {
            TransitionTo(AiraState.MoveToKey);
        }

        // Update logic per frame
        private void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.MINIGAME_PLATFORMER)
                return;

            CheckGrounded();
            UpdateStateMachine();
            UpdateAnimator();
        }

        // Terapkan velocity physics
        private void FixedUpdate()
        {
            if (_state == AiraState.ActAsBase) return;

            if (GameManager.Instance?.CurrentState != GameManager.GameState.MINIGAME_PLATFORMER)
                return;

            MoveTowardsTarget(GetCurrentTarget());
        }

        // Transisi ke state baru
        private void TransitionTo(AiraState newState)
        {
            _state = newState;
            Debug.Log($"[AiraAI] State → {newState}");
        }

        // Logic utama state machine
        private void UpdateStateMachine()
        {
            switch (_state)
            {
                case AiraState.MoveToKey:
                    if (_key == null || !_key.gameObject.activeSelf)
                    {
                        TransitionTo(AiraState.MoveToDoor);
                        return;
                    }
                    if (IsKeyOnHighPlatform())
                        TransitionTo(AiraState.WaitForStack);
                    break;

                case AiraState.WaitForStack:
                    if (_player == null) break;
                    float dist = Vector2.Distance(transform.position, _player.transform.position);
                    if (dist <= _stackWaitRadius)
                        TransitionTo(AiraState.ActAsBase);
                    break;

                case AiraState.ActAsBase:
                    // Unfreeze ditangani StackingSystem via OnPlayerStacking()
                    break;

                case AiraState.MoveToDoor:
                    if (_door == null) break;
                    if (IsNear(_door.transform.position))
                    {
                        if (!_hasSpokenDoor)
                        {
                            _hasSpokenDoor = true;
                            TTSManager.Instance?.EnqueueSpeak("I'm at the door!", "HAPPY");
                        }
                        PlatformerGame.Instance?.NotifyAtDoor(false);
                        TransitionTo(AiraState.Done);
                    }
                    break;
            }
        }

        // Pindah ke target posisi
        private void MoveTowardsTarget(Vector3? target)
        {
            if (!target.HasValue) return;

            float dir = Mathf.Sign(target.Value.x - transform.position.x);

            if (Mathf.Abs(target.Value.x - transform.position.x) > _arriveRadius)
                _rb.linearVelocity = new Vector2(dir * _moveSpeed, _rb.linearVelocity.y);
            else
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            if (target.Value.y > transform.position.y + 0.5f && _isGrounded)
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpForce);

            FlipSprite(dir);
        }

        // Target posisi sesuai state
        private Vector3? GetCurrentTarget()
        {
            return _state switch
            {
                AiraState.MoveToKey    => _key?.transform.position,
                AiraState.WaitForStack => GetStackPosition(),
                AiraState.MoveToDoor   => _door?.transform.position,
                _                      => null
            };
        }

        // Posisi diam jadi base
        private Vector3 GetStackPosition()
        {
            if (_key == null) return transform.position;
            return new Vector3(_key.transform.position.x, transform.position.y, 0f);
        }

        // Cek key di platform tinggi
        private bool IsKeyOnHighPlatform()
        {
            if (_key == null) return false;
            return _key.transform.position.y > transform.position.y + _highPlatformDiff;
        }

        // Cek dekat target
        private bool IsNear(Vector3 target)
        {
            return Vector2.Distance(transform.position, target) <= _arriveRadius;
        }

        // Cek menyentuh tanah
        private void CheckGrounded()
        {
            Transform checkPoint = _groundCheck != null ? _groundCheck : transform;
            _isGrounded = Physics2D.OverlapCircle(checkPoint.position, _groundCheckRadius, _groundLayer);
        }

        // Dipanggil StackingSystem saat player naik
        public void OnPlayerStacking()
        {
            if (_state == AiraState.ActAsBase) return;
            TransitionTo(AiraState.ActAsBase);

            if (!_hasSpokenBase)
            {
                _hasSpokenBase = true;
                TTSManager.Instance?.EnqueueSpeak("I'll be your stepping stone!", "HAPPY");
            }
        }

        // Dipanggil saat player turun
        public void OnPlayerLeft()
        {
            if (_state != AiraState.ActAsBase) return;

            bool keyGone = _key == null || !_key.gameObject.activeSelf;
            TransitionTo(keyGone ? AiraState.MoveToDoor : AiraState.MoveToKey);
        }

        // Flip sprite sesuai arah
        private void FlipSprite(float dir)
        {
            if (_spriteRenderer == null || Mathf.Approximately(dir, 0f)) return;
            _spriteRenderer.flipX = dir < 0f;
        }

        // Update parameter animator
        private void UpdateAnimator()
        {
            if (_animator == null) return;
            _animator.SetBool("isRunning",  Mathf.Abs(_rb.linearVelocity.x) > 0.01f);
            _animator.SetBool("isGrounded", _isGrounded);
        }
    }
}
