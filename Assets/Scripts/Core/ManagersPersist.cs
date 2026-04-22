using UnityEngine;

public class ManagersPersist : MonoBehaviour
{
    // Pertahankan semua manager antar scene
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}