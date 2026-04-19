using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using AIRA.AI;
using AIRA.UI;

public class HeadsUpIntroFlow
{
    // Status intro aktif
    public bool IsActive { get; private set; }

    private HeadsUpGame      _game;
    private HeadsUpUIManager _ui;

    // Step intro
    private enum Step
    {
        WhoFirst, ConfirmWhoFirst,
        WhoPickCategory, ConfirmCategory,
        GeneratingWords
    }

    private Step   _step;
    private bool   _airaGoesFirst;
    private bool   _airaPicksCategory;
    private string _currentCategory;

    // Kategori default untuk random
    private static readonly string[] DefaultCategories =
    {
        "Animals", "Food", "Movies", "Sports",
        "Countries", "Jobs", "Nature", "Technology", "Music"
    };

    // Fallback 5 kata per kategori
    private static readonly Dictionary<string, string[]> FallbackWords = new()
    {
        { "Animals",    new[] { "Cat", "Dog", "Elephant", "Penguin", "Tiger" } },
        { "Food",       new[] { "Pizza", "Sushi", "Banana", "Chocolate", "Rice" } },
        { "Movies",     new[] { "Avatar", "Titanic", "Inception", "Frozen", "Batman" } },
        { "Sports",     new[] { "Soccer", "Tennis", "Swimming", "Basketball", "Running" } },
        { "Countries",  new[] { "Japan", "Brazil", "France", "Egypt", "Canada" } },
        { "Jobs",       new[] { "Doctor", "Teacher", "Pilot", "Chef", "Engineer" } },
        { "Nature",     new[] { "Mountain", "Ocean", "Forest", "Desert", "River" } },
        { "Technology", new[] { "Robot", "Smartphone", "Internet", "Satellite", "Laptop" } },
        { "Music",      new[] { "Guitar", "Piano", "Drums", "Concert", "Album" } },
    };

    // Fallback universal
    private static readonly string[] GenericFallback =
        { "Apple", "House", "Car", "Book", "Tree" };

    // Pool kalimat who goes first - AIRA duluan
    private static readonly string[] _airaFirstPhrases = {
        "[HAPPY] Ooh, I got picked to give clues first! Can I go first? Pretty please?",
        "[HAPPY] I want to give the clues first! Is that okay with you?",
        "[HAPPY] Looks like I go first giving clues! You just need to guess! Deal?"
    };

    // Pool kalimat who goes first - User duluan
    private static readonly string[] _userFirstPhrases = {
        "[HAPPY] You got picked to give clues first! I'll be guessing. Ready?",
        "[HAPPY] Your turn to give me clues first! I'll try my best to guess!",
        "[HAPPY] Looks like you go first! Give me clues and I'll guess the word!"
    };

    // Pool kalimat setuju lanjut
    private static readonly string[] _agreedContinuePhrases = {
        "[HAPPY] Awesome! Let's do it!",
        "[HAPPY] Great, let's go!",
        "[HAPPY] Perfect! Let's get started!"
    };

    // Pool AIRA serah ke user duluan
    private static readonly string[] _airaFirstSwitchPhrases = {
        "[SAD] Oh okay, you go first then! I'll be guessing!",
        "[SAD] Alright, you take the first turn! I'll guess your words!",
        "[NEUTRAL] Okay, you go first! Give me some good clues!"
    };

    // Pool user serah ke AIRA duluan
    private static readonly string[] _userFirstSwitchPhrases = {
        "[SURPRISED] Oh, you want me to go first? Okay, I'll give the clues!",
        "[SURPRISED] Really? I'll go first then! Get ready to guess!",
        "[HAPPY] Sure! I'll give the clues first! You just need to guess!"
    };

    // Pool kalimat AIRA pilih kategori
    private static readonly string[] _airaPicksCategoryPhrases = {
        "[HAPPY] How about we play with '{category}'? Sound good?",
        "[HAPPY] I'm thinking '{category}'! Want to go with that?",
        "[HAPPY] Let's do '{category}'! Are you okay with that?"
    };

    // Pool kalimat user pilih kategori
    private static readonly string[] _userPicksCategoryPhrases = {
        "[THINKING] What category do you want to play? You pick!",
        "[THINKING] Your turn to choose the category! What do you want?",
        "[THINKING] Pick any category you like! What are we playing?"
    };

    // Pool kalimat konfirmasi kategori
    private static readonly string[] _categoryConfirmedPhrases = {
        "[HAPPY] '{category}' it is! Let me think of some words...",
        "[HAPPY] Ooh '{category}'! Great choice! Generating words now!",
        "[HAPPY] '{category}'? Perfect! Give me a second to prepare the words!"
    };

    // Pool AIRA minta kategori lain
    private static readonly string[] _airaRejectCategoryPhrases = {
        "[SHY] Oh, you don't like that? Okay, you pick the category then!",
        "[SHY] Hmm, not that one? What category would you like?",
        "[SHY] Okay okay, your turn to choose! What do you want to play?"
    };

    // Konstruktor terima referensi koordinator
    public HeadsUpIntroFlow(HeadsUpGame game, HeadsUpUIManager ui)
    {
        _game = game;
        _ui   = ui;
    }

    // Mulai intro flow
    public void Start()
    {
        if (IsActive) return;
        IsActive = true;

        ChatUIManager.Instance?.DisplayMessage("user", "Let's Play Heads Up Game!");
        _ui?.ShowGamePanel(false);
        GameManager.Instance?.ChangeState(GameManager.GameState.MINIGAME_INTRO);

        _game.StartCoroutine(RunWhoFirst());
    }

    // Proses input user per step
    public void ProcessInput(string input)
    {
        switch (_step)
        {
            case Step.ConfirmWhoFirst:
                _game.StartCoroutine(HandleConfirmWhoFirst(input));
                break;
            case Step.ConfirmCategory:
                _game.StartCoroutine(HandleConfirmCategory(input));
                break;
        }
    }

    // Coroutine step siapa duluan
    private IEnumerator RunWhoFirst()
    {
        _airaGoesFirst = Random.value > 0.5f;
        string phrase  = _airaGoesFirst ? PickRandom(_airaFirstPhrases) : PickRandom(_userFirstPhrases);

        _step = Step.ConfirmWhoFirst;
        yield return SpeakAndRestore(phrase, GameManager.GameState.MINIGAME_INTRO);
    }

    // Konfirmasi siapa duluan
    private IEnumerator HandleConfirmWhoFirst(string input)
    {
        bool agreed = DetectAgreement(input);
        string phrase;

        if (_airaGoesFirst && !agreed)
        {
            _airaGoesFirst = false;
            phrase = PickRandom(_airaFirstSwitchPhrases);
        }
        else if (!_airaGoesFirst && !agreed)
        {
            _airaGoesFirst = true;
            phrase = PickRandom(_userFirstSwitchPhrases);
        }
        else
        {
            phrase = PickRandom(_agreedContinuePhrases);
        }

        _step = Step.WhoPickCategory;
        yield return SpeakAndRestore(phrase, GameManager.GameState.MINIGAME_INTRO);

        _airaPicksCategory = Random.value > 0.5f;
        yield return _game.StartCoroutine(RunWhoPickCategory());
    }

    // Coroutine tanya/tentukan kategori
    private IEnumerator RunWhoPickCategory()
    {
        string phrase;
        if (_airaPicksCategory)
        {
            _currentCategory = DefaultCategories[Random.Range(0, DefaultCategories.Length)];
            phrase = PickRandom(_airaPicksCategoryPhrases, _currentCategory);
        }
        else
        {
            phrase = PickRandom(_userPicksCategoryPhrases);
        }

        _step = Step.ConfirmCategory;
        yield return SpeakAndRestore(phrase, GameManager.GameState.MINIGAME_INTRO);
    }

    // Konfirmasi kategori
    private IEnumerator HandleConfirmCategory(string input)
    {
        if (_airaPicksCategory)
        {
            bool agreed = DetectAgreement(input);
            if (!agreed)
            {
                _airaPicksCategory = false;
                yield return SpeakAndRestore(PickRandom(_airaRejectCategoryPhrases), GameManager.GameState.MINIGAME_INTRO);
                yield break;
            }
        }
        else
        {
            _currentCategory = input.Trim();
        }

        yield return SpeakAndRestore(PickRandom(_categoryConfirmedPhrases, _currentCategory), GameManager.GameState.MINIGAME_INTRO);

        _step = Step.GeneratingWords;
        yield return _game.StartCoroutine(RunGenerateWordList(_currentCategory));
    }

    // Generate kata via LLM
    private IEnumerator RunGenerateWordList(string category)
    {
        string prompt = $"Generate exactly {_game.RoundWordCount} simple, common words or phrases for the " +
            $"category '{category}' suitable for a guessing game. " +
            $"Return ONLY a numbered list like:\n1. word\n2. word\n...\n{_game.RoundWordCount}. word\n" +
            $"No explanations, no extra text.";

        string response = null;
        var task = LLMManager.Instance.SendMessage(prompt);
        while (!task.IsCompleted) yield return null;

        if (!task.IsFaulted && !task.IsCanceled)
            response = task.Result;

        var wordList = ParseWordList(response);

        if (wordList.Count == 0)
        {
            wordList = GetFallbackWords(category);
            Debug.LogWarning($"[HeadsUpIntroFlow] Parse gagal — fallback '{category}'.");
        }

        IsActive = false;
        _game.OnIntroComplete(_airaGoesFirst, _currentCategory, wordList);
    }

    // Parse response LLM jadi List<string>
    private List<string> ParseWordList(string response)
    {
        if (string.IsNullOrEmpty(response)) return new List<string>();

        var lines  = response.Split('\n');
        var result = new List<string>();
        foreach (var line in lines)
        {
            var cleaned = Regex.Replace(line.Trim(), @"^\d+\.\s*", "");
            if (!string.IsNullOrEmpty(cleaned) && cleaned.Length < 60)
                result.Add(cleaned);
        }
        return result;
    }

    // Ambil fallback kata per kategori
    private List<string> GetFallbackWords(string category)
    {
        foreach (var key in FallbackWords.Keys)
        {
            if (category.ToLower().Contains(key.ToLower()) ||
                key.ToLower().Contains(category.ToLower()))
                return new List<string>(FallbackWords[key]);
        }
        return new List<string>(GenericFallback);
    }

    // Deteksi ya/tidak dari input
    private bool DetectAgreement(string input)
    {
        string lower      = input.ToLower();
        string[] yesWords = { "yes", "yeah", "yep", "sure", "okay",
            "ok", "fine", "go ahead", "of course", "why not" };
        string[] noWords  = { "no", "nope", "nah", "don't", "not",
            "rather", "instead", "you go" };

        foreach (var w in yesWords) if (lower.Contains(w)) return true;
        foreach (var w in noWords)  if (lower.Contains(w)) return false;
        return true;
    }

    // Pilih random dari pool, replace {category}
    private static string PickRandom(string[] pool, string category = "")
    {
        string picked = pool[Random.Range(0, pool.Length)];
        return picked.Replace("{category}", category);
    }

    // Parse tag, ucap, tunggu, restore state
    private IEnumerator SpeakAndRestore(string phraseWithTag, GameManager.GameState restoreState)
    {
        string tag   = TextUtils.ExtractExpressionTag(phraseWithTag);
        string clean = TextUtils.StripExpressionTags(phraseWithTag);
        yield return _game.StartCoroutine(_game.AiraSpeakAndWait(clean, tag.Trim('[', ']')));
        GameManager.Instance?.ChangeState(restoreState);
    }
}
