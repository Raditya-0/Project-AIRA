using UnityEngine;
using UnityEngine.Events;

namespace AIRA.MiniGames.Platformer
{
    public class PressurePlate : MonoBehaviour
    {
        [SerializeField] private Sprite _spriteOff;
        [SerializeField] private Sprite _spriteOn;

        [SerializeField] public string       effectDesc = "opens path";
        [SerializeField] public PlateReactive affects;

        public UnityEvent OnPressed;
        public UnityEvent OnReleased;

        // Status karakter di atas plate
        public bool IsPlayerOn { get; private set; }
        public bool IsAiraOn   { get; private set; }

        private SpriteRenderer _renderer;
        private int _occupantCount;

        // Inisialisasi komponen renderer
        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        // Daftarkan ke registry
        private void OnEnable()
        {
            InteractableRegistry.RegisterPlate(this);
        }

        // Hapus dari registry
        private void OnDisable()
        {
            InteractableRegistry.UnregisterPlate(this);
        }

        // deteksi objek masuk
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))        IsPlayerOn = true;
            if (other.CompareTag("AiraCharacter")) IsAiraOn   = true;

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
            if (other.CompareTag("Player"))        IsPlayerOn = false;
            if (other.CompareTag("AiraCharacter")) IsAiraOn   = false;

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
