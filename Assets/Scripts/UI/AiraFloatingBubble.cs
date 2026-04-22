using System.Collections;
using UnityEngine;
using TMPro;
using AIRA.Voice;

namespace AIRA.UI
{
    public class AiraFloatingBubble : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_Text   _bubbleText;
        [SerializeField] private GameObject _thinkingIndicator;

        [Header("Follow Settings")]
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Transform _headBone;
        [SerializeField] private Vector3   _worldOffset  = new Vector3(0.5f, 2.5f, 0f);
        [SerializeField] private float     _followSpeed  = 8f;
        [SerializeField] private float     _padding      = 20f;
        [SerializeField] private bool      _enableFollow = true;

        private Coroutine     _hideCoroutine;
        private Coroutine     _autoHideCoroutine;
        private RectTransform _rectTransform;
        private Canvas        _canvas;
        private Camera        _mainCamera;
        private CanvasGroup   _canvasGroup;

        // Inisialisasi komponen dan subscribe GameManager
        private void Awake()
        {
            Debug.Log("[AiraFloatingBubble] Awake called.");
            _rectTransform = GetComponent<RectTransform>();
            _canvas        = GetComponentInParent<Canvas>();
            _mainCamera    = Camera.main;

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha         = 0f;
            _canvasGroup.blocksRaycasts = false;

            GameManager.OnStateChanged += HandleStateChanged;
        }

        // Jalankan coroutine subscribe TTS/LLM
        private void Start()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        // Tunggu TTS siap lalu subscribe
        private IEnumerator SubscribeWhenReady()
        {
            Debug.Log("[AiraFloatingBubble] Waiting for TTSManager...");
            yield return new WaitUntil(() => TTSManager.Instance != null);
            Debug.Log("[AiraFloatingBubble] TTSManager found, subscribing.");
            TTSManager.Instance.OnSpeakStart += ShowBubble;
            TTSManager.Instance.OnSpeakEnd   += HideBubble;
            TTSManager.OnSpeakText           += OnSpeakText;
        }

        // Lepas semua event bubble
        private void OnDisable()
        {
            Debug.Log("[AiraFloatingBubble] OnDisable called! Stack: " + System.Environment.StackTrace);
            GameManager.OnStateChanged -= HandleStateChanged;
            TTSManager.OnSpeakText     -= OnSpeakText;

            if (TTSManager.Instance != null)
            {
                TTSManager.Instance.OnSpeakStart -= ShowBubble;
                TTSManager.Instance.OnSpeakEnd   -= HideBubble;
            }
        }

        // Ikuti posisi karakter di canvas
        private void LateUpdate()
        {
            if (!_enableFollow) return;
            if (_canvas == null || _mainCamera == null) return;
            if (_canvasGroup == null || _canvasGroup.alpha == 0f) return;

            Transform anchor = (_headBone != null) ? _headBone : _characterRoot;
            if (anchor == null) return;

            Vector3 worldPos  = anchor.position + anchor.TransformDirection(_worldOffset);
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0f) return;

            Vector2       canvasPos;
            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _mainCamera,
                out canvasPos
            );

            Rect rect = canvasRect.rect;
            canvasPos.x = Mathf.Clamp(canvasPos.x, rect.xMin + _padding, rect.xMax - _padding);
            canvasPos.y = Mathf.Clamp(canvasPos.y, rect.yMin + _padding, rect.yMax - _padding);

            _rectTransform.localPosition = Vector3.Lerp(
                _rectTransform.localPosition,
                new Vector3(canvasPos.x, canvasPos.y, _rectTransform.localPosition.z),
                Time.deltaTime * _followSpeed
            );
        }

        // Terima teks dari TTSManager broadcast
        private void OnSpeakText(string text)
        {
            if (_bubbleText != null) _bubbleText.text = text;
        }

        // Update teks bubble langsung
        public void UpdateText(string text)
        {
            if (_bubbleText != null)
                _bubbleText.text = text;
        }

        // Tampil/sembunyikan elemen thinking
        private void ShowThinking(bool thinking)
        {
            _bubbleText?.gameObject.SetActive(!thinking);
            _thinkingIndicator?.SetActive(thinking);
            if (thinking)
                _canvasGroup.alpha = 1f;
        }

        // Tampilkan bubble dan hentikan hide coroutine
        private void ShowBubble()
        {
            Debug.Log("[AiraFloatingBubble] ShowBubble called.");
            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }
            _canvasGroup.alpha          = 1f;
            _canvasGroup.blocksRaycasts = true;
            ShowThinking(false);
        }

        // Tampilkan bubble dengan auto-hide
        public void ShowDialogBubble(string text, float duration = 3f)
        {
            if (_bubbleText != null)
                _bubbleText.text = TextUtils.StripExpressionTags(text);
            ShowBubble();
            if (_autoHideCoroutine != null)
                StopCoroutine(_autoHideCoroutine);
            _autoHideCoroutine = StartCoroutine(AutoHide(duration));
        }

        // Sembunyikan bubble
        private void HideBubble()
        {
            _canvasGroup.alpha          = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        // Auto-hide setelah durasi tertentu
        private IEnumerator AutoHide(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            HideBubble();
            _autoHideCoroutine = null;
        }

        // Reaksi bubble sesuai state game
        private void HandleStateChanged(GameManager.GameState prev, GameManager.GameState next)
        {
            switch (next)
            {
                case GameManager.GameState.THINKING:
                    ShowThinking(true);
                    break;

                case GameManager.GameState.SPEAKING:
                    ShowThinking(false);
                    _canvasGroup.alpha          = 1f;
                    _canvasGroup.blocksRaycasts = true;
                    break;

                case GameManager.GameState.IDLE:
                    HideBubble();
                    break;

                case GameManager.GameState.MINIGAME_PLATFORMER:
                    // Bubble tetap aktif di Platformer
                    break;
            }
        }
    }
}
