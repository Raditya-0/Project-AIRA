using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIRA.AI;
using AIRA.Character;
using AIRA.UI;
using AIRA.Voice;

public class HeadsUpGame : MiniGameBase
{
    // Singleton global HeadsUpGame
    public static HeadsUpGame Instance { get; private set; }

    [Header("Referensi")]
    [SerializeField] private HeadsUpUIManager _ui;

    [Header("Settings")]
    [SerializeField] private int   _roundWordCount   = 10;
    [SerializeField] private float _roundTimeSeconds = 60f;

    // Properti dibaca IntroFlow dan PlayFlow
    public int   RoundWordCount   => _roundWordCount;
    public float RoundTimeSeconds => _roundTimeSeconds;

    private HeadsUpIntroFlow _introFlow;
    private HeadsUpPlayFlow  _playFlow;

    // Awake init singleton dan flow
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _introFlow = new HeadsUpIntroFlow(this, _ui);
        _playFlow  = new HeadsUpPlayFlow(this, _ui);
    }

    // Daftar listener state game
    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleStateChanged;
    }

    // Lepas listener state game
    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    // Update timer saat bermain
    private void Update()
    {
        if (_playFlow.IsActive)
            _playFlow.UpdateTimer();
    }

    // Entry point dari Play Button
    public override void StartGame() => _introFlow.Start();

    // Entry point dari GameManager
    public override void ProcessUserResponse(string input)
    {
        if (_introFlow.IsActive)
            _introFlow.ProcessInput(input);
        else if (_playFlow.IsActive)
            _playFlow.ProcessInput(input);
    }

    // Selesai game
    public override void EndGame() => OnGameEnd();

    // Apakah game sedang berjalan
    public override bool IsGameActive => _introFlow.IsActive || _playFlow.IsActive;

    // Dipanggil IntroFlow saat selesai
    public void OnIntroComplete(bool isAiraTurn, string category, List<string> words)
        => _playFlow.StartRound(isAiraTurn, category, words);

    // Dipanggil PlayFlow saat game selesai
    public void OnGameEnd()
        => GameManager.Instance?.ChangeState(GameManager.GameState.IDLE);

    // Ucapkan teks langsung tanpa LLM
    public void AiraSpeakDirect(string text, string expression = "NEUTRAL")
    {
        string cleaned = TextUtils.StripEmoji(text);
        ChatUIManager.Instance?.DisplayMessage("aira", cleaned);
        ChatUIManager.Instance?.ShowDialogBubble(cleaned, 4f);
        AiraController.Instance?.SetExpression(expression);
        GameManager.Instance?.ChangeState(GameManager.GameState.SPEAKING);
        TTSManager.Instance?.EnqueueSpeak(cleaned, expression);
    }

    // Ucapkan teks dan tunggu TTS selesai
    public IEnumerator AiraSpeakAndWait(string text, string expression = "NEUTRAL")
    {
        AiraSpeakDirect(text, expression);
        yield return null; // Beri waktu coroutine start
        if (TTSManager.Instance != null)
            yield return new WaitUntil(() => !TTSManager.Instance.IsSpeaking);
    }

    // Sembunyikan panel saat keluar minigame
    private void HandleStateChanged(GameManager.GameState prev, GameManager.GameState next)
    {
        if (!IsGameActive) return;

        bool isMiniGameContext =
            next == GameManager.GameState.MINIGAME_INTRO   ||
            next == GameManager.GameState.MINIGAME_PLAYING ||
            next == GameManager.GameState.MINIGAME_RESULT  ||
            next == GameManager.GameState.THINKING         ||
            next == GameManager.GameState.SPEAKING;

        if (!isMiniGameContext)
            _ui?.ShowGamePanel(false);
    }
}
