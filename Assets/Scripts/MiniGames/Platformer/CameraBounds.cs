using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public class CameraBounds : MonoBehaviour
    {
        // ── Fields ──────────────────────────────────────────────────────────

        [SerializeField] private Camera _camera;
        [SerializeField] private float  _paddingX = 0.3f;
        [SerializeField] private float  _paddingY = 0.3f;

        private Rigidbody2D _rb;

        // ── Properties ──────────────────────────────────────────────────────

        // Batas kiri world space
        public float LeftBound  { get; private set; }

        // Batas kanan world space
        public float RightBound { get; private set; }

        // ── Unity Lifecycle ─────────────────────────────────────────────────

        // Inisialisasi komponen
        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            UpdateBounds();
        }

        // Clamp posisi setelah physics
        private void LateUpdate()
        {
            UpdateBounds();

            Vector3 min = _camera.ViewportToWorldPoint(new Vector3(0, 0, 0));
            Vector3 max = _camera.ViewportToWorldPoint(new Vector3(1, 1, 0));

            float clampedX = Mathf.Clamp(_rb.position.x, min.x + _paddingX, max.x - _paddingX);
            float clampedY = Mathf.Clamp(_rb.position.y, min.y + _paddingY, max.y - _paddingY);
            _rb.position = new Vector2(clampedX, clampedY);
        }

        // ── Private Methods ─────────────────────────────────────────────────

        // Hitung ulang batas kamera
        private void UpdateBounds()
        {
            Vector3 min = _camera.ViewportToWorldPoint(new Vector3(0, 0, 0));
            Vector3 max = _camera.ViewportToWorldPoint(new Vector3(1, 1, 0));

            LeftBound  = min.x + _paddingX;
            RightBound = max.x - _paddingX;
        }
    }
}
