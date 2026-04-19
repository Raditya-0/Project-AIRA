using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using AIRA.Emotion;
using AIRA.UI;

public class LoadingGate : MonoBehaviour
{
    // Singleton global LoadingGate
    public static LoadingGate Instance { get; private set; }

    [Header("Referensi")]
    [SerializeField] private ChatUIManager     _chatUIManager;
    [SerializeField] private Button            _playButton;
    [SerializeField] private EmotionClassifier _emotionClassifier;
    [SerializeField] private LoadingScreenUI   _loadingScreenUI;

    [Header("Timeout Fallback")]
    [SerializeField] private float _timeoutSeconds = 30f;

    // Status ready tiap sistem
    private bool _llmReady     = false;
    private bool _ttsReady     = false;
    private bool _sttReady     = false;
    private bool _emotionReady = false;

    // Semua sistem ready
    public bool IsReady => _llmReady && _ttsReady && _sttReady
        && (_emotionClassifier == null || _emotionReady);

    // Awake lock input awal
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Aktifkan loading screen di awal
        _loadingScreenUI?.gameObject.SetActive(true);

        // Lock input dari awal
        _chatUIManager?.SetInputLocked(true);
        if (_playButton != null) _playButton.interactable = false;
    }

    // Start mulai timer timeout
    private void Start() => StartCoroutine(TimeoutCoroutine());

    // LLM siap dipakai
    public void SetLLMReady()
    {
        _llmReady = true;
        _loadingScreenUI?.SetProgress(0.6f, "Model bahasa siap...");
        CheckAllReady();
    }

    // TTS siap dipakai
    public void SetTTSReady()
    {
        _ttsReady = true;
        _loadingScreenUI?.SetProgress(0.8f, "Text-to-Speech siap...");
        CheckAllReady();
    }

    // STT siap dipakai
    public void SetSTTReady()
    {
        _sttReady = true;
        _loadingScreenUI?.SetProgress(0.95f, "Speech Recognition siap...");
        CheckAllReady();
    }

    // Emotion classifier siap
    public void SetEmotionReady() { _emotionReady = true; CheckAllReady(); }

    // Cek semua ready
    private void CheckAllReady()
    {
        if (!IsReady) return;

        // Unlock input semua sistem ready
        _loadingScreenUI?.SetProgress(1f, "Selamat datang!");
        _chatUIManager?.SetInputLocked(false);
        if (_playButton != null) _playButton.interactable = true;
        Debug.Log("[LoadingGate] Semua model ready.");
    }

    // Timeout fallback force unlock
    private IEnumerator TimeoutCoroutine()
    {
        yield return new WaitForSeconds(_timeoutSeconds);
        if (!IsReady)
        {
            // Force unlock meskipun belum semua ready
            Debug.LogWarning("[LoadingGate] Timeout — force unlock input.");
            _loadingScreenUI?.SetProgress(1f, "Siap (timeout)...");
            _chatUIManager?.SetInputLocked(false);
            if (_playButton != null) _playButton.interactable = true;
        }
    }
}
