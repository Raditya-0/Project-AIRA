using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public class LevelExit : MonoBehaviour
    {
        private bool _playerInside;
        private bool _airaInside;

        // Exit aktif jika key diambil
        private bool CanActivate =>
            PlatformerGame.Instance != null && PlatformerGame.Instance.KeyCollected;

        // Daftar event key collected
        private void OnEnable()  => PlatformerGame.OnKeyCollected += TriggerIfBothInside;

        // Batalkan event key collected
        private void OnDisable() => PlatformerGame.OnKeyCollected -= TriggerIfBothInside;

        // Catat entitas masuk zona
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))             _playerInside = true;
            else if (other.CompareTag("AiraCharacter")) _airaInside   = true;
            TriggerIfBothInside();
        }

        // Catat entitas keluar zona
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))             _playerInside = false;
            else if (other.CompareTag("AiraCharacter")) _airaInside   = false;
        }

        // Aktifkan exit jika terpenuhi
        private void TriggerIfBothInside()
        {
            if (!CanActivate || !_playerInside || !_airaInside) return;
            PlatformerGame.Instance.NotifyEndReached();
        }
    }
}
