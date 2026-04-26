using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AIRA.AI;
using AIRA.Character;
using AIRA.Voice;

namespace AIRA.UI
{
public class ChatUIManager : MonoBehaviour
{
    public static ChatUIManager Instance { get; private set; }

    // Inspector References
    [Header("Input Row")]
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button         _sendButton;
    [SerializeField] private Button         _cancelButton;

    [Header("Voice Input")]
    [SerializeField] private MicToggleButton _micToggle;

    [Header("Chat History")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Transform  _chatContent;   // parent of bubble instances

    [Header("Bubble Prefabs")]
    [SerializeField] private GameObject _userBubblePrefab;
    [SerializeField] private GameObject _aiBubblePrefab;

    // Private State
    private Coroutine _errorRecoveryCoroutine;


    // Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _sendButton  ?.onClick.AddListener(OnUserSubmit);
        _cancelButton?.onClick.AddListener(OnCancel);

        if (_inputField != null)
            _inputField.onSubmit.AddListener(OnInputFieldSubmit);

        ShowCancelButton(false);
    }

    // Subscribe event state
    private void OnEnable()
    {
        GameManager.OnStateChanged += HandleStateChanged;
    }

    // Lepas event state
    private void OnDisable()
    {
        GameManager.OnStateChanged -= HandleStateChanged;
    }

    // Bersihkan singleton saat destroy
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Input Handling
    public void OnUserSubmit()
    {
        if (_inputField == null) return;

        string text = _inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _inputField.text = "";
        DisplayMessage("user", text);

        AiraController.Instance?.ResetIdleTimer();

        GameManager.Instance?.ProcessUserInput(text);
    }

    private void OnInputFieldSubmit(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        OnUserSubmit();
        _inputField.ActivateInputField();
    }

    public void OnCancel()
    {
        LLMManager.Instance?.CancelCurrent();
        GameManager.Instance?.ChangeState(GameManager.GameState.IDLE);
        Debug.Log("[ChatUIManager] User cancelled.");
    }

    // Wrapper ke TextUtils.StripEmoji
    public static string StripEmoji(string text) => TextUtils.StripEmoji(text);

    // Display
    public void DisplayMessage(string role, string content)
    {
        string displayText = AiraController.StripExpressionTags(content);
        displayText = Regex.Replace(displayText, @"\p{Cs}|\p{So}", "").Trim();

        bool        isUser = role == "user";
        GameObject  prefab = isUser ? _userBubblePrefab : _aiBubblePrefab;

        if (prefab == null || _chatContent == null)
        {
            Debug.Log($"[ChatUIManager] [{role}]: {displayText}");
            return;
        }

        var bubble = Instantiate(prefab, _chatContent);

        var chatBubble = bubble.GetComponent<ChatBubble>();
        if (chatBubble != null)
            chatBubble.SetText(displayText);
        else
        {
            var label = bubble.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = displayText;
        }

        var bubbleRect = bubble.GetComponent<RectTransform>();
        if (bubbleRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleRect);

        var contentRect = _chatContent.GetComponent<RectTransform>();
        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 0f;

        StartCoroutine(ScrollToBottomNextFrame());
    }

    // UI State Helpers
    public void SetInputLocked(bool locked)
    {
        if (_inputField != null) _inputField.interactable = !locked;
        if (_sendButton != null) _sendButton.interactable  = !locked;
    }

    public void ShowCancelButton(bool show)
    {
        if (_cancelButton != null)
            _cancelButton.gameObject.SetActive(show);
    }

    // State Machine Listener
    private void HandleStateChanged(GameManager.GameState prev, GameManager.GameState next)
    {
        if (_errorRecoveryCoroutine != null)
        {
            StopCoroutine(_errorRecoveryCoroutine);
            _errorRecoveryCoroutine = null;
        }

        switch (next)
        {
            case GameManager.GameState.IDLE:
            case GameManager.GameState.LISTENING:
                SetInputLocked(false);
                ShowCancelButton(false);
                break;

            case GameManager.GameState.THINKING:
                SetInputLocked(true);
                ShowCancelButton(true);
                break;

            case GameManager.GameState.SPEAKING:
                SetInputLocked(true);
                ShowCancelButton(false);
                break;

            case GameManager.GameState.MINIGAME_INTRO:
            case GameManager.GameState.MINIGAME_PLAYING:
                SetInputLocked(false);
                ShowCancelButton(false);
                break;

            case GameManager.GameState.MINIGAME_RESULT:
                SetInputLocked(true);
                ShowCancelButton(false);
                break;

            case GameManager.GameState.ERROR:
                SetInputLocked(true);
                ShowCancelButton(false);
                _errorRecoveryCoroutine = StartCoroutine(RecoverFromError(2f));
                break;
        }
    }

    // Coroutine Helpers
    private IEnumerator RecoverFromError(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetInputLocked(false);
        GameManager.Instance?.ChangeState(GameManager.GameState.IDLE);
        _errorRecoveryCoroutine = null;
        Debug.Log("[ChatUIManager] Recovered from ERROR state.");
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 0f;
    }
}
}
