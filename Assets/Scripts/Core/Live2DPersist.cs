using UnityEngine;

public class Live2DCameraPersist : MonoBehaviour
{
    private static Live2DCameraPersist s_instance;

    // Pertahankan kamera Live2D antar scene
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