using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatUIManager : MonoBehaviour
{
    // Inspector References
    [Header("Input Row")]
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button         _sendButton;
    [SerializeField] private Button         _cancelButton;

    [Header("Chat History")]
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Transform  _chatContent;   // parent of bubble instances

    [Header("Bubble Prefabs")]
    [SerializeField] private GameObject _userBubblePrefab;
    [SerializeField] private GameObject _aiBubblePrefab;

    [Header("Dialog Bubble")]
    [SerializeField] private GameObject      _dialogBubble;
    [SerializeField] private TextMeshProUGUI _dialogBubbleText;
    [SerializeField] private GameObject      _thinkingIndicatorInBubble;

    [Header("Thinking Indicator")]
    [SerializeField] private GameObject _thinkingIndicator; // Legacy indicator

    // Private State
    private Coroutine _dialogHideCoroutine;
    private Coroutine _errorRecoveryCoroutine;
    private bool      _bubbleShowingThinking;


    // Unity Lifecycle
    private void Awake()
    {
        _sendButton  ?.onClick.AddListener(OnUserSubmit);
        _cancelButton?.onClick.AddListener(OnCancel);

        if (_inputField != null)
            _inputField.onSubmit.AddListener(OnInputFieldSubmit);

        ShowCancelButton(false);
        ShowThinkingIndicator(false);
        if (_thinkingIndicatorInBubble != null)
            _thinkingIndicatorInBubble.SetActive(false);
        SetDialogBubbleVisible(false);
    }

    private void OnEnable()  => GameManager.OnStateChanged += HandleStateChanged;
    private void OnDisable() => GameManager.OnStateChanged -= HandleStateChanged;

    // Input Handling
    public void OnUserSubmit()
    {
        if (_inputField == null) return;

        string text = _inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _inputField.text = "";
        DisplayMessage("user", text);

        AICommentator.Instance?.ResetIdleTimer();

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

    // Display
    public void DisplayMessage(string role, string content)
    {
        string displayText = AiraController.StripExpressionTags(content);

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
    public void ShowDialogBubble(string text, float duration = 3f)
    {
        if (_dialogBubble == null) return;

        ShowBubbleThinking(false);

        if (_dialogBubbleText != null)
            _dialogBubbleText.text = text;

        SetDialogBubbleVisible(true);

        if (_dialogHideCoroutine != null) StopCoroutine(_dialogHideCoroutine);
        _dialogHideCoroutine = StartCoroutine(HideDialogBubbleAfter(duration));
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

    public void ShowThinkingIndicator(bool show)
    {
        if (_thinkingIndicator != null)
            _thinkingIndicator.SetActive(show);
    }

    private void ShowBubbleThinking(bool thinking)
    {
        _bubbleShowingThinking = thinking;

        if (_dialogBubbleText != null)
            _dialogBubbleText.gameObject.SetActive(!thinking);

        if (_thinkingIndicatorInBubble != null)
            _thinkingIndicatorInBubble.SetActive(thinking);

        if (thinking)
            SetDialogBubbleVisible(true);
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
                ShowThinkingIndicator(false);
                if (_bubbleShowingThinking)
                {
                    ShowBubbleThinking(false);
                    SetDialogBubbleVisible(false);
                }
                break;

            case GameManager.GameState.THINKING:
                SetInputLocked(true);
                ShowCancelButton(true);
                ShowThinkingIndicator(true);
                if (_dialogHideCoroutine != null)
                {
                    StopCoroutine(_dialogHideCoroutine);
                    _dialogHideCoroutine = null;
                }
                ShowBubbleThinking(true);
                break;

            case GameManager.GameState.SPEAKING:
                SetInputLocked(true);
                ShowCancelButton(false);
                ShowThinkingIndicator(false);
                ShowBubbleThinking(false);
                break;

            case GameManager.GameState.ERROR:
                SetInputLocked(true);
                ShowCancelButton(false);
                ShowThinkingIndicator(false);
                ShowBubbleThinking(false);
                _errorRecoveryCoroutine = StartCoroutine(RecoverFromError(2f));
                break;
        }
    }

    // Coroutine Helpers
    private void SetDialogBubbleVisible(bool visible)
    {
        if (_dialogBubble != null)
            _dialogBubble.SetActive(visible);
    }

    private IEnumerator HideDialogBubbleAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetDialogBubbleVisible(false);
        _dialogHideCoroutine = null;
    }

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
