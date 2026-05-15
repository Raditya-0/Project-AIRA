using UnityEngine;

public class MinigameLauncher : MonoBehaviour
{
    // Hubungkan ke tombol Platformer di Inspector
    public void PlayPlatformer()
    {
        GameManager.Instance?.StartPlatformer();
    }

    // Hubungkan ke tombol SpaceShooter di Inspector
    public void PlaySpaceShooter()
    {
        GameManager.Instance?.StartSpaceShooter();
    }

    // Hubungkan ke tombol HeadsUp di Inspector
    public void PlayHeadsUp()
    {
        GameManager.Instance?.StartHeadsUp();
    }
}
