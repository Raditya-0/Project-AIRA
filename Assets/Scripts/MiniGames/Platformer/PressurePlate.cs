using UnityEngine;
using UnityEngine.Events;

namespace AIRA.MiniGames.Platformer
{
    public class PressurePlate : MonoBehaviour
    {
        [SerializeField] private Sprite _spriteOff;
        [SerializeField] private Sprite _spriteOn;

        public UnityEvent OnPressed;
        public UnityEvent OnReleased;

        private SpriteRenderer _renderer;
        private int _occupantCount;

        // inisialisasi komponen renderer
        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        // deteksi objek masuk
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsValid(other)) return;

            _occupantCount++;
            if (_occupantCount == 1)
            {
                _renderer.sprite = _spriteOn;
                OnPressed?.Invoke();
            }
        }

        // deteksi objek keluar
        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsValid(other)) return;

            _occupantCount = Mathf.Max(0, _occupantCount - 1);
            if (_occupantCount == 0)
            {
                _renderer.sprite = _spriteOff;
                OnReleased?.Invoke();
            }
        }

        // validasi tag objek
        private bool IsValid(Collider2D other)
        {
            return other.CompareTag("Player") || other.CompareTag("AiraCharacter");
        }
    }
}
