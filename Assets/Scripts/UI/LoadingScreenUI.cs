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

    private float _currentTarget = 0f;
    private bool  _fadingOut     = false;

    // Inisialisasi tampilan awal
    private void Start()
    {
        if (_progressSlider != null) _progressSlider.value = 0f;
        if (_canvasGroup    != null) _canvasGroup.alpha    = 1f;
    }

    // Smooth slider menuju target
    private void Update()
    {
        if (_progressSlider == null) return;
        _progressSlider.value = Mathf.Lerp(
            _progressSlider.value,
            _currentTarget,
            Time.deltaTime * _sliderSmoothing);
    }

    // Set target progress dan status
    public void SetProgress(float target, string statusMessage)
    {
        _currentTarget = Mathf.Clamp01(target);
        if (_statusText != null) _statusText.text = statusMessage;

        if (_currentTarget >= 1f && !_fadingOut)
        {
            _fadingOut = true;
            StartCoroutine(FadeOut());
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
