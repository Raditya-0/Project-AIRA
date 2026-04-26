using UnityEngine;

namespace AIRA.MiniGames.SpaceShooter
{

public class DestroyAfterEffect : MonoBehaviour
{
    [SerializeField] private float m_delay = 2f; // Durasi VFX kamu (misal 2 detik)

    private void Start()
    {
        // Langsung jadwalkan penghancuran objek saat ia muncul
        Destroy(gameObject, m_delay);
    }
}
}