using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter
{
    public class SpaceShooterEntryPoint : MiniGameBase
    {
        // Apakah game aktif
        public override bool IsGameActive => true;

        // Stub — state dikelola GameManager
        public override void StartGame() { }

        // Kembali ke MainScene
        public override void EndGame()
        {
            GameManager.Instance?.EndSpaceShooter();
        }

        // Forward input ke LLM pipeline
        public override void ProcessUserResponse(string input)
        {
            GameManager.Instance?.SendToLLM(input);
        }

        // Inisialisasi state saat masuk
        private void Start()
        {
            GameManager.Instance?.RegisterMiniGame(this);
            GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_SPACESHOOTER);
        }

        // Dipanggil tombol exit UI
        public void ExitGame() => EndGame();
    }
}
