using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using AIRA.AI;
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

        // Flag level sedang menyelesaikan routine
        public bool IsCompletingLevel => _isCompleting;
        private bool _isCompleting;

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

        // Panggil StartGame saat scene load
        private void Start()
        {
            StartGame();
        }

        // Hancurkan instance saat destroy
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Mulai game level
        public override void StartGame()
        {
            GameManager.Instance?.RegisterMiniGame(this);
            KeyCollected  = false;
            EndReached    = false;
            _isActive     = true;
            _isCompleting = false;

            if (_player != null && _playerSpawn != null)
            {
                var rb = _player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.gravityScale = 0f;
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                    rb.position = _playerSpawn.position;
                    rb.gravityScale = 3f;
                }
                _player.transform.position = _playerSpawn.position;

                // Snap player ke ground pakai raycast
                var rbSnap = _player.GetComponent<Rigidbody2D>();
                RaycastHit2D hit = Physics2D.Raycast(
                    _playerSpawn.position,
                    Vector2.down,
                    10f,
                    LayerMask.GetMask("Ground")
                );
                if (hit.collider != null)
                {
                    float snapY = hit.point.y + 0.5f;
                    rbSnap.position = new Vector2(_playerSpawn.position.x, snapY);
                    _player.transform.position = new Vector2(_playerSpawn.position.x, snapY);
                }
            }

            if (_airaAI != null && _airaSpawn != null)
                _airaAI.transform.position = _airaSpawn.position;

            _key?.gameObject.SetActive(true);
            _endPoint?.ResetEndPoint();

            Debug.Log("[PlatformerGame] Level dimulai.");
        }

        // Akhiri game level
        public override void EndGame()
        {
            _isActive = false;
            GameManager.Instance?.UnregisterMiniGame();
            LoadNextLevelOrEnd(SceneManager.GetActiveScene().name);
        }

        // Muat level atau akhiri platformer
        public void LoadNextLevelOrEnd(string currentSceneName)
        {
            string[] levels = { "Platformer_Level01", "Platformer_Level02" };
            int index = System.Array.IndexOf(levels, currentSceneName);
            if (index >= 0 && index < levels.Length - 1)
                SceneManager.LoadScene(levels[index + 1]);
            else
                GameManager.Instance?.EndPlatformer();
        }

        // Forward input ke LLM pipeline
        public override void ProcessUserResponse(string input)
        {
            GameManager.Instance?.SendToLLM(input);
        }

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
            _isActive     = false;
            _isCompleting = true;

            STTManager.Instance?.StopListening();
            LLMManager.Instance?.CancelCurrent();

            // Tentukan next level untuk konteks komentar
            int nextLevel = GetNextLevelIndex();
            PlatformerCommentator.Instance?.OnLevelTransition(nextLevel);

            yield return new WaitUntil(() =>
                TTSManager.Instance == null || !TTSManager.Instance.IsSpeaking);
            yield return new WaitForSeconds(0.5f);

            LoadNextLevelOrEnd(SceneManager.GetActiveScene().name);
        }

        // Hitung index level berikutnya
        private int GetNextLevelIndex()
        {
            string[] levels  = { "Platformer_Level01", "Platformer_Level02" };
            string   current = SceneManager.GetActiveScene().name;
            int      index   = System.Array.IndexOf(levels, current);
            return index >= 0 ? index + 2 : levels.Length + 1;
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
