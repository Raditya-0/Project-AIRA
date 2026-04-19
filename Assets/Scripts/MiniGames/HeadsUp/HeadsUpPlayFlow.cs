using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIRA.AI;

public class HeadsUpPlayFlow
{
    // Status play aktif
    public bool IsActive { get; private set; }

    private HeadsUpGame      _game;
    private HeadsUpUIManager _ui;

    private bool         _isAiraTurn;
    private string       _currentCategory;
    private List<string> _wordList;
    private int          _currentWordIndex;
    private int          _correctCount;
    private int          _skipCount;
    private float        _timer;
    private bool         _isPlaying;

    // Simpan raw response LLM terakhir
    private string _lastAiraResponse;

    // Suffix instruksi semua prompt LLM
    private const string PromptSuffix = " Keep response under 15 words. No emoji.";

    // Pool announce AIRA kasih clue
    private static readonly string[] _airaGivesClueAnnounce = {
        "[HAPPY] Okay! I'll give you clues, you guess the word! Here we go!",
        "[HAPPY] My turn to give clues! Listen carefully and guess!",
        "[HAPPY] I'll describe the word, you tell me what it is! Ready?"
    };

    // Pool announce User kasih clue
    private static readonly string[] _userGivesClueAnnounce = {
        "[HAPPY] Now you give me clues, I'll try to guess the word!",
        "[HAPPY] Your turn! Describe the word to me without saying it!",
        "[HAPPY] Give me hints and I'll guess! Don't say the word directly!"
    };

    // Konstruktor terima referensi koordinator
    public HeadsUpPlayFlow(HeadsUpGame game, HeadsUpUIManager ui)
    {
        _game = game;
        _ui   = ui;
    }

    // Mulai round
    public void StartRound(bool isAiraTurn, string category, List<string> words)
    {
        IsActive          = true;
        _isAiraTurn       = isAiraTurn;
        _currentCategory  = category;
        _wordList         = words;
        _currentWordIndex = 0;
        _correctCount     = 0;
        _skipCount        = 0;
        _timer            = _game.RoundTimeSeconds;
        _isPlaying        = false;

        _ui?.UpdateCategory(_currentCategory);
        _ui?.UpdateScore(_correctCount, _skipCount);

        _game.StartCoroutine(RunStartRound());
    }

    // Update timer — dipanggil dari HeadsUpGame.Update()
    public void UpdateTimer()
    {
        if (!_isPlaying) return;

        _timer -= Time.deltaTime;
        _ui?.UpdateTimer(_timer);

        if (_timer <= 0f)
        {
            _timer = 0f;
            EndRound();
        }
    }

    // Proses input user
    public void ProcessInput(string input)
    {
        if (!_isPlaying) return;

        string currentWord = _wordList[_currentWordIndex];

        if (_isAiraTurn)
        {
            if (DetectCorrectAnswer(input, currentWord))
            {
                _correctCount++;
                _game.StartCoroutine(PraiseAndNext(correct: true));
            }
            else if (DetectSkip(input))
            {
                _skipCount++;
                _game.StartCoroutine(PraiseAndNext(correct: false));
            }
            else
            {
                // Retry clue berbeda
                _game.StartCoroutine(AiraGiveClue(currentWord, isRetry: true));
            }
        }
        else
        {
            // User kasih clue, AIRA tebak via LLM
            _game.StartCoroutine(AiraGuess(input, currentWord));
        }
    }

    // Coroutine announce dan mulai round
    private IEnumerator RunStartRound()
    {
        _currentWordIndex = -1;

        string phrase = _isAiraTurn
            ? PickRandom(_airaGivesClueAnnounce)
            : PickRandom(_userGivesClueAnnounce);

        string tag   = TextUtils.ExtractExpressionTag(phrase);
        string clean = TextUtils.StripExpressionTags(phrase);

        yield return _game.StartCoroutine(_game.AiraSpeakAndWait(clean, tag.Trim('[', ']')));

        _isPlaying = true;
        _ui?.ShowGamePanel(true);
        GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_PLAYING);
        NextWord();
    }

    // AIRA kasih clue untuk kata
    private IEnumerator AiraGiveClue(string word, bool isRetry = false)
    {
        _isPlaying = false;
        GameManager.Instance?.ChangeState(GameManager.GameState.THINKING);

        string retryHint = isRetry ? "Give a different, easier clue this time." : "";
        string prompt = $"Give ONE short clue (max 10 words) for the word '{word}' " +
            $"without saying the word itself. Category: {_currentCategory}. " +
            $"Be playful and cute! {retryHint} Use appropriate expression tag.";

        yield return _game.StartCoroutine(SpeakLLMResponse(prompt));
        _isPlaying = true;
        GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_PLAYING);
    }

    // AIRA tebak dari clue user
    private IEnumerator AiraGuess(string userClue, string targetWord)
    {
        _isPlaying = false;
        GameManager.Instance?.ChangeState(GameManager.GameState.THINKING);

        string prompt = $"The user gave this clue: '{userClue}'. " +
            $"Category is '{_currentCategory}'. " +
            $"Make ONE guess for the word. Say it naturally like 'Is it a [word]?' " +
            $"Use [THINKING] tag.";

        yield return _game.StartCoroutine(SpeakLLMResponse(prompt));

        if (DetectCorrectAnswer(_lastAiraResponse, targetWord))
        {
            _correctCount++;
            yield return _game.StartCoroutine(AiraCelebrate());
        }
        else
        {
            // Tebak salah, minta clue lagi
            string retryPrompt = "You guessed wrong. Ask the user for another clue. Use [SHY] tag.";
            yield return _game.StartCoroutine(SpeakLLMResponse(retryPrompt));
            _isPlaying = true;
            GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_PLAYING);
        }
    }

    // Pujian dan next kata
    private IEnumerator PraiseAndNext(bool correct)
    {
        _isPlaying = false;
        GameManager.Instance?.ChangeState(GameManager.GameState.THINKING);

        string prompt = correct
            ? $"The user correctly guessed '{_wordList[_currentWordIndex]}'! " +
              $"Praise them with varied, enthusiastic compliments. " +
              $"Examples: 'You're so smart!', 'Wow, you got it!', 'Amazing!' " +
              $"Use [HAPPY] tag. Keep it short, 1 sentence."
            : $"The user skipped '{_wordList[_currentWordIndex]}'. " +
              $"Say it's okay encouragingly. Use [NEUTRAL] tag. Keep it short.";

        yield return _game.StartCoroutine(SpeakLLMResponse(prompt));
        NextWord();
        if (_isPlaying)
            GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_PLAYING);
    }

    // AIRA celebrate tebak benar
    private IEnumerator AiraCelebrate()
    {
        GameManager.Instance?.ChangeState(GameManager.GameState.THINKING);

        string prompt = $"You correctly guessed '{_wordList[_currentWordIndex]}'! " +
            $"Celebrate cutely. Use [HAPPY] or [SURPRISED] tag. Keep it short.";

        yield return _game.StartCoroutine(SpeakLLMResponse(prompt));
        NextWord();
        if (_isPlaying)
            GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_PLAYING);
    }

    // Lanjut kata berikutnya
    private void NextWord()
    {
        _currentWordIndex++;
        if (_currentWordIndex >= _wordList.Count || _timer <= 0f)
        {
            EndRound();
            return;
        }

        _isPlaying = true;
        _ui?.UpdateScore(_correctCount, _skipCount);
        _ui?.ShowWord(_isAiraTurn
            ? "???"
            : _wordList[_currentWordIndex]);

        if (_isAiraTurn)
            _game.StartCoroutine(AiraGiveClue(_wordList[_currentWordIndex]));
    }

    // Selesai round
    private void EndRound()
    {
        if (!IsActive) return;
        IsActive   = false;
        _isPlaying = false;
        _ui?.ShowGamePanel(false);
        GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_RESULT);
        _game.StartCoroutine(RunRequestAiraComment());
    }

    // Request komentar hasil dari LLM
    private IEnumerator RunRequestAiraComment()
    {
        string prompt = $"The Head's Up round is over! " +
            $"Score: {_correctCount} correct and {_skipCount} skipped out of {_wordList.Count} words. " +
            $"Category was '{_currentCategory}'. " +
            $"Give a fun, energetic comment about the results! Use [HAPPY] or [SURPRISED] tag.";

        GameManager.Instance?.ChangeState(GameManager.GameState.THINKING);
        yield return _game.StartCoroutine(SpeakLLMResponse(prompt, restoreState: GameManager.GameState.IDLE));
        _game.OnGameEnd();
    }

    // Helper panggil LLM dan ucap hasilnya
    private IEnumerator SpeakLLMResponse(string prompt,
        GameManager.GameState restoreState = GameManager.GameState.MINIGAME_PLAYING)
    {
        string response = null;
        var task = LLMManager.Instance.SendMessage(prompt + PromptSuffix);
        while (!task.IsCompleted) yield return null;

        if (this == null) yield break;

        if (!task.IsFaulted && !task.IsCanceled)
            response = task.Result;

        if (string.IsNullOrEmpty(response))
            response = "[HAPPY] Okay!";

        response         = TextUtils.StripEmoji(response);
        _lastAiraResponse = response;

        string tag   = TextUtils.ExtractExpressionTag(response);
        string clean = TextUtils.StripExpressionTags(response);

        yield return _game.StartCoroutine(_game.AiraSpeakAndWait(clean, tag.Trim('[', ']')));
    }

    // Deteksi jawaban benar dari input
    private bool DetectCorrectAnswer(string input, string targetWord)
    {
        if (string.IsNullOrEmpty(input)) return false;
        string inputLower  = input.ToLower();
        string targetLower = targetWord.ToLower();

        if (inputLower.Contains(targetLower)) return true;

        // Fuzzy — cek tiap kata utama
        var targetWords = targetLower.Split(' ');
        foreach (var w in targetWords)
            if (w.Length > 3 && inputLower.Contains(w)) return true;

        return false;
    }

    // Deteksi skip dari input
    private bool DetectSkip(string input)
    {
        string[] skipWords = {
            "skip", "next", "pass", "don't know",
            "no idea", "give up", "idk", "dunno"
        };
        string lower = input.ToLower();
        foreach (var w in skipWords)
            if (lower.Contains(w)) return true;
        return false;
    }

    // Pilih random dari pool
    private static string PickRandom(string[] pool)
    {
        return pool[Random.Range(0, pool.Length)];
    }
}
