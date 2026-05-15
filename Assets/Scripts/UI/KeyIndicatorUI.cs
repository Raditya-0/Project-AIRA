using UnityEngine;
using UnityEngine.UI;
using AIRA.MiniGames.Platformer;

namespace AIRA.UI
{
    public class KeyIndicatorUI : MonoBehaviour
    {
        [Header("Sprite Kunci")]
        [SerializeField] private Image  _keyImage;
        [SerializeField] private Sprite _spriteEmpty;
        [SerializeField] private Sprite _spriteCollected;

        // Subscribe event kunci
        private void OnEnable()
        {
            PlatformerGame.OnKeyCollected += OnKeyCollected;
        }

        // Unsubscribe event kunci
        private void OnDisable()
        {
            PlatformerGame.OnKeyCollected -= OnKeyCollected;
        }

        // Inisialisasi sprite awal
        private void Start()
        {
            if (_keyImage != null)
                _keyImage.sprite = _spriteEmpty;

            if (PlatformerGame.Instance?.KeyCollected == true)
                SetCollected();
        }

        // Kunci berhasil diambil
        private void OnKeyCollected()
        {
            SetCollected();
        }

        // Swap sprite ke collected
        private void SetCollected()
        {
            if (_keyImage != null)
                _keyImage.sprite = _spriteCollected;
        }
    }
}
