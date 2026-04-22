using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public class EndPoint : MonoBehaviour
    {
        private bool _playerInside = false;
        private bool _airaInside   = false;
        private bool _triggered    = false;

        // Deteksi masuk trigger
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))        _playerInside = true;
            if (other.CompareTag("AiraCharacter")) _airaInside   = true;
            CheckBothInside();
        }

        // Deteksi keluar sebelum keduanya masuk
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))        _playerInside = false;
            if (other.CompareTag("AiraCharacter")) _airaInside   = false;
        }

        // Cek kedua entitas di dalam
        private void CheckBothInside()
        {
            if (_triggered || !_playerInside || !_airaInside) return;
            _triggered = true;
            PlatformerGame.Instance?.NotifyEndReached();
        }

        // Reset flag untuk replay
        public void ResetEndPoint()
        {
            _playerInside = _airaInside = _triggered = false;
        }
    }
}
