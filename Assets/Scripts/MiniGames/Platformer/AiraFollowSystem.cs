using System;
using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class AiraFollowSystem : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Transform _player;
        [SerializeField] private float     _followDistance = 1.5f;
        [SerializeField] private float     _moveSpeed      = 4f;

        [Header("Idle Timeouts")]
        [SerializeField] private float _idleTimeout1 = 20f;
        [SerializeField] private float _idleTimeout2 = 60f;

        [Header("Jump Settings")]
        [SerializeField] private AiraVisionSystem _visionSystem;
        [SerializeField] private float     _jumpForce   = 7f;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Edge & Step Detection")]
        [SerializeField] private float _maxFollowYDiff  = 3f;
        [SerializeField] private float _edgeRayDistance = 1f;
        [SerializeField] private float _edgeRayOffset   = 0.4f;
        [SerializeField] private float _stepUpThreshold = 0.5f;
        [SerializeField] private float _stepUpForce     = 2f;

        [Header("Fall Detection")]
        [SerializeField] private float _fallThresholdY = -10f;

        [Header("Follow Mode")]
        [SerializeField] public FollowMode _followMode = FollowMode.FollowPlayer;

        public enum FollowMode { FollowPlayer, LLMDriven }

        // Event idle player
        public static event Action OnPlayerIdle20s;
        public static event Action OnPlayerIdle60s;
        public static event Action OnPlayerResumed;

        // Event jatuh ke jurang
        public static event Action OnAiraFellIntoGap;
        public static event Action OnPlayerFellIntoGap;

        private Rigidbody2D    _rb;
        private SpriteRenderer _spriteRenderer;

        private float   _playerIdleTime;
        private Vector2 _lastPlayerPos;
        private bool    _isOnPlayerHead;
        private bool    _idle20Fired;
        private bool    _idle60Fired;
        private bool    _isGrounded;
        private bool    _playerFallFired;
        private bool    _airaFallFired;

        // Ambil komponen
        private void Awake()
        {
            _rb             = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Simpan posisi awal player
        private void Start()
        {
            if (_player != null)
                _lastPlayerPos = _player.position;
        }

        // Update follow dan idle tracking
        private void Update()
        {
            if (_followMode != FollowMode.FollowPlayer) return;
            if (_player == null) return;
            if (GameManager.Instance?.CurrentState != GameManager.GameState.MINIGAME_PLATFORMER)
                return;

            TrackPlayerIdle();

            // Deteksi player jatuh ke jurang
            if (!_playerFallFired && _player.position.y < _fallThresholdY)
            {
                _playerFallFired = true;
                OnPlayerFellIntoGap?.Invoke();
            }

            if (!_airaFallFired && transform.position.y < _fallThresholdY)
            {
                _airaFallFired = true;
                OnAiraFellIntoGap?.Invoke();
            }
        }

        // Terapkan movement follow
        private void FixedUpdate()
        {
            if (_followMode != FollowMode.FollowPlayer) return;
            if (_player == null) return;
            if (_isOnPlayerHead) return;
            if (GameManager.Instance?.CurrentState != GameManager.GameState.MINIGAME_PLATFORMER)
                return;

            MoveToFollow();
        }

        // Pantau apakah player diam
        private void TrackPlayerIdle()
        {
            float moved = Vector2.Distance(_player.position, _lastPlayerPos);

            if (moved > 0.05f)
            {
                if (_idle20Fired || _idle60Fired)
                {
                    _idle20Fired    = false;
                    _idle60Fired    = false;
                    _isOnPlayerHead = false;
                    OnPlayerResumed?.Invoke();
                }
                _playerIdleTime = 0f;
                _lastPlayerPos  = _player.position;
                return;
            }

            _playerIdleTime += Time.deltaTime;

            if (_playerIdleTime >= _idleTimeout2 && !_idle60Fired)
            {
                _idle60Fired = true;
                MoveToPlayerHead();
                OnPlayerIdle60s?.Invoke();
            }
            else if (_playerIdleTime >= _idleTimeout1 && !_idle20Fired)
            {
                _idle20Fired = true;
                FacePlayer();
                OnPlayerIdle20s?.Invoke();
            }
        }

        // Gerak ikut player dengan edge detection
        private void MoveToFollow()
        {
            float distX = Mathf.Abs(_player.position.x - transform.position.x);
            float distY = Mathf.Abs(_player.position.y - transform.position.y);

            // Henti saat Y terlalu beda
            if (distY > _maxFollowYDiff)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            if (distX <= _followDistance) return;

            float dir = Mathf.Sign(_player.position.x - transform.position.x);

            // Stop saat ada jurang
            if (IsEdgeAhead(dir))
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                return;
            }

            _rb.linearVelocity = new Vector2(dir * _moveSpeed, _rb.linearVelocity.y);

            // Step-up atau lompat saat ada obstacle
            if (_visionSystem != null && _visionSystem.HasObstacleAhead)
            {
                if (Mathf.Sign(_visionSystem.NearestObstacleDirection) == dir)
                {
                    if (CanStepUp(dir))
                        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _stepUpForce);
                    else
                        TryJump();
                }
            }

            FlipSprite(dir);
        }

        // Deteksi jurang di depan Aira
        private bool IsEdgeAhead(float dir)
        {
            Vector2 origin = new Vector2(
                transform.position.x + dir * _edgeRayOffset,
                transform.position.y - 0.3f
            );
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, _edgeRayDistance, _groundLayer);
            return !hit;
        }

        // Cek Aira bisa step-up obstacle
        private bool CanStepUp(float dir)
        {
            Vector2 feetPos = new Vector2(transform.position.x, transform.position.y - 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(feetPos, new Vector2(dir, 0f), 0.3f, _groundLayer);
            if (!hit) return false;

            float obstacleTopY   = hit.collider.bounds.max.y;
            float obstacleHeight = obstacleTopY - feetPos.y;

            Vector2 topCheckPos = new Vector2(hit.point.x + dir * 0.1f, obstacleTopY + 0.2f);
            bool topClear = Physics2D.OverlapCircle(topCheckPos, 0.15f, _groundLayer) == null;

            return topClear && obstacleHeight < _stepUpThreshold;
        }

        // Cek grounded lalu lompat
        private void TryJump()
        {
            _isGrounded = Physics2D.OverlapCircle(
                new Vector2(transform.position.x, transform.position.y - 0.5f),
                0.15f,
                _groundLayer
            );

            if (_isGrounded)
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpForce);
        }

        // Hadap ke arah player
        private void FacePlayer()
        {
            if (_spriteRenderer == null || _player == null) return;
            float dir = _player.position.x - transform.position.x;
            _spriteRenderer.flipX = dir < 0f;
        }

        // Set flag naik ke kepala player
        private void MoveToPlayerHead()
        {
            _isOnPlayerHead = true;
        }

        // Hentikan follow dari luar
        public void SetEnabled(bool enabled)
        {
            this.enabled = enabled;
            if (!enabled) _rb.linearVelocity = Vector2.zero;
        }

        // Flip sprite sesuai arah gerak
        private void FlipSprite(float dir)
        {
            if (_spriteRenderer == null || Mathf.Approximately(dir, 0f)) return;
            _spriteRenderer.flipX = dir < 0f;
        }
    }
}
