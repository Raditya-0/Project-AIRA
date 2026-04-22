using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float _delay = 1f;

    // Hancurkan objek setelah delay
    private void Start()
    {
        Destroy(gameObject, _delay);
    }
}
