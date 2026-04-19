using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public class Door : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color          _lockedColor   = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private Color          _unlockedColor = Color.white;

        [Header("Collider")]
        [SerializeField] private Collider2D _triggerCollider;

        private bool _isLocked = true;

        // Inisialisasi visual terkunci
        private void Awake()
        {
            ApplyVisual();
        }

        // Set status terkunci/terbuka
        public void SetLocked(bool locked)
        {
            _isLocked = locked;
            ApplyVisual();
        }

        // Terapkan warna sesuai status
        private void ApplyVisual()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = _isLocked ? _lockedColor : _unlockedColor;
        }

        // Deteksi pemain di depan door
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isLocked) return;

            if (!other.CompareTag("Player") && !other.CompareTag("AiraCharacter"))
                return;

            bool isPlayer = other.CompareTag("Player");
            PlatformerGame.Instance?.NotifyAtDoor(isPlayer);
        }
    }
}
