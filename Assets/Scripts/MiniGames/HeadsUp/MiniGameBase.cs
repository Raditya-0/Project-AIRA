using UnityEngine;

public abstract class MiniGameBase : MonoBehaviour
{
    // Mulai game
    public abstract void StartGame();

    // Selesai game
    public abstract void EndGame();

    // Proses input user
    public abstract void ProcessUserResponse(string input);

    // Apakah game aktif
    public abstract bool IsGameActive { get; }
}
