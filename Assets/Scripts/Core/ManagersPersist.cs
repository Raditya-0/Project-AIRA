using UnityEngine;

public class ManagersPersist : MonoBehaviour
{
    private static ManagersPersist s_instance;

    // Pertahankan semua manager antar scene
    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }
}