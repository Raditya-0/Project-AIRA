using System.Linq;
using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public class AiraPlanner : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────

        public static AiraPlanner Instance { get; private set; }

        // ── Fields ───────────────────────────────────────────────────────────

        [SerializeField] private AiraVisionSystem  _vision;
        [SerializeField] private AiraFollowSystem  _followSystem;
        [SerializeField] private AiraAIController  _airaAI;
        [SerializeField] private PlayerController  _player;
        [SerializeField] private float             _evalInterval       = 0.5f;
        [SerializeField] private float             _playerHintTimeout  = 5f;
        [SerializeField] private float             _playerTakeOverTime = 10f;
        [SerializeField] private float             _holdReleaseTimeout = 8f;

        private enum PlannerState
        {
            Idle,
            WaitingForPlayer,
            GoingToPlate,
            HoldingPlate,
            Done
        }

        private PlannerState  _state = PlannerState.Idle;
        private float         _evalTimer;
        private float         _hintTimer;
        private float         _holdTimer;
        private bool          _hintFired;
        private PressurePlate _relevantButton;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        // Inisialisasi singleton
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // Evaluasi situasi periodik
        private void Update()
        {
            if (_state == PlannerState.Done) return;
            _evalTimer += Time.deltaTime;
            if (_evalTimer < _evalInterval) return;
            _evalTimer = 0f;
            EvaluateSituation();
        }

        // ── State Machine ────────────────────────────────────────────────────

        // Cari button yang relevan untuk coop
        private void FindRelevantButton()
        {
            _relevantButton = _vision.VisibleButtons
                .FirstOrDefault(b => b.affects != null);
        }

        // Evaluasi situasi level
        private void EvaluateSituation()
        {
            FindRelevantButton();

            switch (_state)
            {
                case PlannerState.Idle:
                    if (_relevantButton != null) HandleIdle();
                    break;
                case PlannerState.WaitingForPlayer:
                    HandleWaitingForPlayer();
                    break;
                case PlannerState.GoingToPlate:
                    HandleGoingToPlate();
                    break;
                case PlannerState.HoldingPlate:
                    HandleHoldingPlate();
                    break;
            }
        }

        // Evaluasi saat idle
        private void HandleIdle()
        {
            if (_relevantButton == null) return;

            var playerPlate = _vision.VisibleButtons
                .FirstOrDefault(b => b.IsPlayerOn);

            if (playerPlate != null)
            {
                // Player sudah injak plate, Aira ke plate lain
                var airaTarget = _vision.VisibleButtons
                    .Where(b => b != playerPlate && !b.IsPlayerOn && !b.IsAiraOn)
                    .OrderBy(b => Vector2.Distance(transform.position, b.transform.position))
                    .FirstOrDefault();

                if (airaTarget != null)
                {
                    _followSystem?.OverrideTarget(airaTarget.transform);
                    _state = PlannerState.GoingToPlate;
                    PlatformerCommentator.Instance?.OnAiraGoingToPlate();
                }
                return;
            }

            // Tidak ada yang injak — Aira ke plate terdekat ke dirinya
            var nearest = _vision.VisibleButtons
                .Where(b => !b.IsAiraOn)
                .OrderBy(b => Vector2.Distance(transform.position, b.transform.position))
                .FirstOrDefault();

            if (nearest == null) return;

            _followSystem?.OverrideTarget(nearest.transform);
            _state = PlannerState.GoingToPlate;
            PlatformerCommentator.Instance?.OnAiraGoingToPlate();
        }

        // Handle state WaitingForPlayer
        private void HandleWaitingForPlayer()
        {
            _hintTimer += _evalInterval;

            if (_relevantButton != null && _relevantButton.IsPlayerOn)
            {
                // Player nahan plate, Aira jalan lewat ke endpoint
                _followSystem?.OverrideTarget(_vision.EndPointTransform);
                _state = PlannerState.Done;
                Debug.Log("[AiraPlanner] Player nahan plate, Aira jalan lewat");
                PlatformerCommentator.Instance?.OnCoopSuccess();
                return;
            }

            if (_hintTimer >= _playerTakeOverTime)
            {
                _followSystem?.OverrideTarget(_relevantButton.transform);
                _state = PlannerState.GoingToPlate;
                Debug.Log("[AiraPlanner] GoingToPlate (takeover)");
                PlatformerCommentator.Instance?.OnAiraFrustratedIgnored();
                return;
            }

            if (!_hintFired && _hintTimer >= _playerHintTimeout)
            {
                _hintFired = true;
                PlatformerCommentator.Instance?.OnAiraGoingToPlate();
            }
        }

        // Handle state GoingToPlate
        private void HandleGoingToPlate()
        {
            if (_relevantButton != null && _relevantButton.IsAiraOn)
            {
                _airaAI?.StartHoldingPlate();
                _followSystem?.ClearOverride();
                _holdTimer = 0f;
                _state = PlannerState.HoldingPlate;
                Debug.Log("[AiraPlanner] HoldingPlate");
                PlatformerCommentator.Instance?.OnAiraHoldingPlate();
            }
        }

        // Cek kondisi lepas plate
        private void HandleHoldingPlate()
        {
            _holdTimer += _evalInterval;

            bool wallGone = _vision.NearestBlockingWall == null;
            bool timedOut = _holdTimer >= _holdReleaseTimeout;

            if (wallGone || timedOut)
            {
                _airaAI?.StopHoldingPlate();
                _followSystem?.SetEnabled(true);
                _followSystem?.ClearOverride();
                _state = PlannerState.Done;
                Debug.Log("[AiraPlanner] Coop berhasil");
                PlatformerCommentator.Instance?.OnCoopSuccess();
                return;
            }

            // Wall masih blocking — cek Aira terpeleset
            if (_relevantButton != null && !_relevantButton.IsAiraOn)
            {
                _airaAI?.StopHoldingPlate();
                _followSystem?.OverrideTarget(_relevantButton.transform);
                _holdTimer = 0f;
                _state = PlannerState.GoingToPlate;
                Debug.Log("[AiraPlanner] Aira terpeleset, GoingToPlate");
            }
        }

        // ── Public Methods ───────────────────────────────────────────────────

        // Dipanggil AiraFollowSystem saat tiba di target
        public void OnAiraArrivedAtPlate()
        {
            if (_state != PlannerState.GoingToPlate) return;
            if (_relevantButton != null && _relevantButton.IsAiraOn) return;
            _airaAI?.StartHoldingPlate();
            _followSystem?.ClearOverride();
            _holdTimer = 0f;
            _state = PlannerState.HoldingPlate;
            Debug.Log("[AiraPlanner] HoldingPlate (arrived)");
            PlatformerCommentator.Instance?.OnAiraHoldingPlate();
        }
    }
}
