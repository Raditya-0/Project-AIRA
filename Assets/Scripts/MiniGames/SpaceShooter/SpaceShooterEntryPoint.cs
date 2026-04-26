using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter
{
    public class SpaceShooterEntryPoint : MonoBehaviour
    {
        // Inisialisasi state saat masuk
        private void Start()
        {
            GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_SPACESHOOTER);
        }

        // Keluar kembali ke MainScene
        public void ExitGame()
        {
            GameManager.Instance?.EndSpaceShooter();
        }
    }
}
