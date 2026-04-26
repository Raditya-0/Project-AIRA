using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class AiraAIController : MonoBehaviour
    {
        // State machine Aira
        private enum AiraState
        {
            Idle,
            Following,
            ActAsBase,
            OnPlayerHead
        }

        [Header("References")]
        [SerializeField] private AiraFollowSystem  _followSystem;
        [SerializeField] private Animator          _animator;

        private Rigidbody2D _rb;
        private AiraState   _state = AiraState.Idle;

        // Inisialisasi komponen
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        // Mulai follow saat start
        private void Start()
        {
            TransitionTo(AiraState.Following);
        }

        // Sinkron animator setiap frame
        private void Update()
        {
            if (GameManager.Instance?.CurrentState != GameManager.GameState.MINIGAME_PLATFORMER)
                return;

            UpdateAnimator();
        }

        // Transisi ke state baru
        private void TransitionTo(AiraState newState)
        {
            _state = newState;
            Debug.Log($"[AiraAI] State → {newState}");

            switch (newState)
            {
                case AiraState.Following:
                    _followSystem?.SetEnabled(true);
                    _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    break;

                case AiraState.ActAsBase:
                    _followSystem?.SetEnabled(false);
                    _rb.constraints = RigidbodyConstraints2D.FreezeAll;
                    break;

                case AiraState.OnPlayerHead:
                    _followSystem?.SetEnabled(false);
                    break;

                case AiraState.Idle:
                    _followSystem?.SetEnabled(false);
                    _rb.linearVelocity = Vector2.zero;
                    break;
            }
        }

        // Dipanggil StackingSystem saat player naik
        public void OnPlayerStacking()
        {
            if (_state == AiraState.ActAsBase) return;
            TransitionTo(AiraState.ActAsBase);
            PlatformerCommentator.Instance?.OnStacking();
        }

        // Dipanggil saat player turun
        public void OnPlayerLeft()
        {
            if (_state != AiraState.ActAsBase) return;
            TransitionTo(AiraState.Following);
        }

        // Dipanggil AiraFollowSystem saat idle 60s
        public void OnMoveToPlayerHead()
        {
            TransitionTo(AiraState.OnPlayerHead);
        }

        // Dipanggil saat player bergerak lagi
        public void OnResumeFollowing()
        {
            TransitionTo(AiraState.Following);
        }

        // Update parameter animator
        private void UpdateAnimator()
        {
            if (_animator == null) return;
            _animator.SetBool("isRunning",  Mathf.Abs(_rb.linearVelocity.x) > 0.01f);
            _animator.SetBool("isGrounded", _state != AiraState.OnPlayerHead);
            _animator.SetFloat("yVelocity", _rb.linearVelocity.y);
        }
    }
}
