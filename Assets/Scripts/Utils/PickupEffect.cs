using UnityEngine;

// Putar animasi burst lalu destroy
public class PickupEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] burstFrames;
    [SerializeField] private float frameDuration = 0.05f;

    private SpriteRenderer sr;
    private int currentFrame;
    private float timer;

    // Inisialisasi komponen
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    // Mulai frame pertama
    private void Start()
    {
        sr = GetComponent<SpriteRenderer>(); // pastikan ini ada di Start juga
        Debug.Log($"[PickupEffect] burstFrames: {burstFrames?.Length}, sr: {sr}");
        
        if (burstFrames == null || burstFrames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }
        sr.sprite = burstFrames[0];
    }

    // Update frame per frame
    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= frameDuration)
        {
            timer = 0f;
            currentFrame++;
            if (currentFrame >= burstFrames.Length)
            {
                Destroy(gameObject);
                return;
            }
            sr.sprite = burstFrames[currentFrame];
        }
    }
}