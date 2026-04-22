using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AIRA.UI;
using AIRA.Voice;

namespace AIRA.MiniGames.Platformer
{
    public class PlatformerGame : MiniGameBase
    {
        // Singleton global
        public static PlatformerGame Instance { get; private set; }

        // Event broadcast state level
        public static event Action OnKeyCollected;
        public static event Action OnEndReached;

        [Header("References")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private AiraAIController _airaAI;
        [SerializeField] private KeyPickup        _key;
        [SerializeField] private EndPoint         _endPoint;
        [SerializeField] private Transform        _playerSpawn;
        [SerializeField] private Transform        _airaSpawn;

        // State level
        public bool KeyCollected { get; private set; }
        public bool EndReached   { get; private set; }

        // Apakah game aktif
        public override bool IsGameActive => _isActive;
        private bool _isActive;

        // Awake singleton setup
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Hancurkan instance saat destroy
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Mulai game level
        public override void StartGame()
        {
            KeyCollected = false;
            EndReached   = false;
            _isActive    = true;

            if (_player != null && _playerSpawn != null)
                _player.transform.position = _playerSpawn.position;

            if (_airaAI != null && _airaSpawn != null)
                _airaAI.transform.position = _airaSpawn.position;

            _key?.gameObject.SetActive(true);
            _endPoint?.ResetEndPoint();

            GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_PLATFORMER);
            Debug.Log("[PlatformerGame] Level dimulai.");
        }

        // Akhiri game level
        public override void EndGame()
        {
            _isActive = false;
            GameManager.Instance?.EndPlatformer();
        }

        // Input user diabaikan (game controller)
        public override void ProcessUserResponse(string input) { }

        // Catat key berhasil diambil
        public void NotifyKeyCollected()
        {
            if (KeyCollected) return;
            KeyCollected = true;
            Debug.Log("[PlatformerGame] Key collected.");
            OnKeyCollected?.Invoke();
            CheckLevelComplete();
        }

        // Catat kedua entitas di endpoint
        public void NotifyEndReached()
        {
            if (EndReached) return;
            EndReached = true;
            Debug.Log("[PlatformerGame] End reached.");
            OnEndReached?.Invoke();
            CheckLevelComplete();
        }

        // Cek kondisi level selesai
        private void CheckLevelComplete()
        {
            if (!KeyCollected || !EndReached) return;
            StartCoroutine(LevelCompleteRoutine());
        }

        // Komentar AIRA lalu selesai
        private IEnumerator LevelCompleteRoutine()
        {
            _isActive = false;
            AiraSpeak("We did it! Level complete!", "HAPPY");
            // Tunggu TTS selesai
            yield return new WaitUntil(() => !TTSManager.Instance.IsSpeaking);
            // Delay supaya tidak terlalu tiba-tiba
            yield return new WaitForSeconds(1f);
            GameManager.Instance?.EndPlatformer();
        }

        // Update teks bubble dan jalankan TTS
        private void AiraSpeak(string text, string expression = "HAPPY")
        {
            string clean = TextUtils.StripExpressionTags(text);
            FindFirstObjectByType<AiraFloatingBubble>()?.UpdateText(clean);
            TTSManager.Instance?.EnqueueSpeak(text, expression);
        }

        // Handle aksi dari komponen lain
        public void OnPlayerAction(string action, object data)
        {
            switch (action)
            {
                case "key_collected": NotifyKeyCollected();        break;
                case "stacking":      _airaAI?.OnPlayerStacking(); break;
            }
        }
    }
}
