using UnityEngine;
using AIRA.Voice;

namespace AIRA.MiniGames.Platformer
{
    public class KeyPickup : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private GameObject     _pickupEffect;

        // Deteksi pemain menyentuh key
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player") && !other.CompareTag("AiraCharacter"))
                return;

            bool isAira = other.CompareTag("AiraCharacter");
            if (isAira)
                TTSManager.Instance?.EnqueueSpeak("Got the key!", "HAPPY");

            // Spawn effect tanpa Animator
            if (_pickupEffect != null)
                Instantiate(_pickupEffect, transform.position, Quaternion.identity);

            PlatformerGame.Instance?.NotifyKeyCollected();
            gameObject.SetActive(false);
        }
    }
}