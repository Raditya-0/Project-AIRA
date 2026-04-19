using UnityEngine;
using AIRA.AI;
using AIRA.Voice;

public class AIRASettings : MonoBehaviour
{
    // Singleton instance global
    public static AIRASettings Instance { get; private set; }

    [Header("AI & Memory")]
    [SerializeField] private bool _useEmotionClassifier = false;

    [Header("Voice")]
    [SerializeField] private bool  _ttsEnabled = true;
    [SerializeField] private bool  _sttEnabled = true;
    [SerializeField] [Range(0f, 1f)] private float _ttsVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool _showDebugLog = true;

    [Header("Manager References")]
    [SerializeField] private TTSManager  _ttsManager;
    [SerializeField] private STTManager  _sttManager;
    [SerializeField] private LLMManager  _llmManager;

    // Properti akses dari luar
    public bool  UseEmotionClassifier => _useEmotionClassifier;
    public bool  TTSEnabled           => _ttsEnabled;
    public bool  STTEnabled           => _sttEnabled;
    public float TTSVolume            => _ttsVolume;
    public bool  ShowDebugLog         => _showDebugLog;

    // Inisialisasi singleton
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Terapkan settings saat mulai
    private void Start()
    {
        ApplySettings();
    }

    // Broadcast nilai ke manager
    [ContextMenu("Apply Settings")]
    public void ApplySettings()
    {
        if (_ttsManager != null)
        {
            _ttsManager.enabled = _ttsEnabled;
            var audio = _ttsManager.GetComponent<AudioSource>();
            if (audio != null) audio.volume = _ttsVolume;
        }

        if (_sttManager != null)
            _sttManager.enabled = _sttEnabled;

        // LLMManager butuh public setter EmotionClassifier
        if (_llmManager != null && _showDebugLog)
            Debug.Log($"[AIRASettings] UseEmotionClassifier = {_useEmotionClassifier}");

        if (_showDebugLog)
            Debug.Log("[AIRASettings] Settings applied.");
    }
}
