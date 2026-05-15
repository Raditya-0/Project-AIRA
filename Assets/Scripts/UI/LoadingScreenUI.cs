using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingScreenUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Slider      _progressSlider;
    [SerializeField] private TMP_Text    _statusText;
    [SerializeField] private TMP_Text    _titleText;

    [Header("Tuning")]
    [SerializeField] private float _fadeDuration    = 1f;
    [SerializeField] private float _sliderSmoothing = 3f;

    [Header("Background")]
    [SerializeField] private Image  _backgroundImage;
    [SerializeField] private Sprite _backgroundSprite;

    [Header("Hint Rotation")]
    [SerializeField] private TMP_Text _hintText;
    [SerializeField] private float    _hintInterval     = 4f;
    [SerializeField] private float    _hintFadeDuration = 0.4f;
    [SerializeField] private string[] _hints = new string[]
    {
        "Aira can hear the emotion in your tone of voice. Try talking with different expressions!",
        "You can click on Aira to see her react!",
        "Aira remembers your previous conversations.",
        "Try playing mini games with Aira for a fun experience!",
        "Speak naturally, Aira understands casual conversation."
    };

    private float _currentTarget = 0f;
    private bool  _fadingOut     = false;
    private int   _hintIndex     = 0;

    // Inisialisasi tampilan awal
    private void Start()
    {
        if (_progressSlider != null) _progressSlider.value = 0f;
        if (_canvasGroup    != null) _canvasGroup.alpha    = 1f;

        if (_backgroundSprite != null && _backgroundImage != null)
            _backgroundImage.sprite = _backgroundSprite;

        if (_hintText != null && _hints != null && _hints.Length > 0)
        {
            _hintText.text  = _hints[0];
            _hintText.alpha = 1f;
            StartCoroutine(HintRotationCoroutine());
        }
    }

    // Set background dari luar
    public void SetBackground(Sprite sprite)
    {
        if (_backgroundImage != null && sprite != null)
            _backgroundImage.sprite = sprite;
    }

    // Set hints dari luar per scene
    public void SetHints(string[] hints)
    {
        if (hints != null && hints.Length > 0)
            _hints = hints;
    }

    // Smooth slider dan persentase teks
    private void Update()
    {
        if (_progressSlider == null) return;
        _progressSlider.value = Mathf.Lerp(
            _progressSlider.value,
            _currentTarget,
            Time.deltaTime * _sliderSmoothing);

        if (_statusText != null)
            _statusText.text = $"{Mathf.RoundToInt(_progressSlider.value * 100f)}%";
    }

    // Set target progress, abaikan statusMessage
    public void SetProgress(float target, string statusMessage)
    {
        _currentTarget = Mathf.Clamp01(target);

        if (_currentTarget >= 1f && !_fadingOut)
        {
            _fadingOut = true;
            StartCoroutine(FadeOut());
        }
    }

    // Rotasi hint berurutan dengan fade
    private IEnumerator HintRotationCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_hintInterval);

            if (_fadingOut) yield break;

            float elapsed = 0f;
            while (elapsed < _hintFadeDuration)
            {
                elapsed += Time.deltaTime;
                if (_hintText != null)
                    _hintText.alpha = Mathf.Lerp(1f, 0f, elapsed / _hintFadeDuration);
                yield return null;
            }

            _hintIndex = (_hintIndex + 1) % _hints.Length;
            if (_hintText != null) _hintText.text = _hints[_hintIndex];

            elapsed = 0f;
            while (elapsed < _hintFadeDuration)
            {
                elapsed += Time.deltaTime;
                if (_hintText != null)
                    _hintText.alpha = Mathf.Lerp(0f, 1f, elapsed / _hintFadeDuration);
                yield return null;
            }

            if (_hintText != null) _hintText.alpha = 1f;
        }
    }

    // Fade alpha ke nol lalu nonaktif
    private IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(0.5f);

        float elapsed = 0f;
        float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _fadeDuration);
            yield return null;
        }

        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
