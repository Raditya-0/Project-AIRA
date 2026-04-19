using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AIRA.Voice;

namespace AIRA.MiniGames.Platformer
{
    public class PlatformerGame : MiniGameBase
    {
        // Singleton global
        public static PlatformerGame Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PlayerController _player;
        [SerializeField] private AiraAIController _airaAI;
        [SerializeField] private KeyPickup        _key;
        [SerializeField] private Door             _door;
        [SerializeField] private Transform        _playerSpawn;
        [SerializeField] private Transform        _airaSpawn;

        [Header("Settings")]
        [SerializeField] private float _levelCompleteDelay = 2f;

        // State level
        public bool KeyCollected  { get; private set; }
        public bool PlayerAtDoor  { get; private set; }
        public bool AiraAtDoor    { get; private set; }

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
            PlayerAtDoor = false;
            AiraAtDoor   = false;
            _isActive    = true;

            if (_player != null && _playerSpawn != null)
                _player.transform.position = _playerSpawn.position;

            if (_airaAI != null && _airaSpawn != null)
                _airaAI.transform.position = _airaSpawn.position;

            _key?.gameObject.SetActive(true);
            _door?.SetLocked(true);

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
            _door?.SetLocked(false);
            Debug.Log("[PlatformerGame] Key collected.");
            CheckLevelComplete();
        }

        // Catat siapa yang sampai door
        public void NotifyAtDoor(bool isPlayer)
        {
            if (isPlayer) PlayerAtDoor = true;
            else          AiraAtDoor   = true;
            CheckLevelComplete();
        }

        // Cek kondisi level selesai
        private void CheckLevelComplete()
        {
            if (!KeyCollected || !PlayerAtDoor || !AiraAtDoor) return;
            StartCoroutine(LevelCompleteRoutine());
        }

        // Komentar AIRA lalu selesai
        private IEnumerator LevelCompleteRoutine()
        {
            _isActive = false;
            TTSManager.Instance?.EnqueueSpeak("We did it! Level complete!", "HAPPY");
            yield return new WaitForSeconds(_levelCompleteDelay);
            EndGame();
        }

        // Handle aksi dari komponen lain
        public void OnPlayerAction(string action, object data)
        {
            switch (action)
            {
                case "key_collected": NotifyKeyCollected();          break;
                case "at_door":       NotifyAtDoor((bool)data);      break;
                case "stacking":      _airaAI?.OnPlayerStacking();   break;
            }
        }
    }
}
