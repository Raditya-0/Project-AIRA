using System.Collections;
using UnityEngine;

public class ThinkingIndicator : MonoBehaviour
{
    [Header("Dot RectTransforms")]
    [SerializeField] private RectTransform _dot1;
    [SerializeField] private RectTransform _dot2;
    [SerializeField] private RectTransform _dot3;

    [Header("Animation Settings")]
    [SerializeField] private float _bounceHeight  = 8f;
    [SerializeField] private float _bounceDuration = 0.5f;
    [SerializeField] private float _staggerDelay  = 0.15f;

    // Lifecycle
    private void OnEnable()  => StartCoroutine(AnimateLoop());
    private void OnDisable() => ResetDots();

    // Animation
    private IEnumerator AnimateLoop()
    {
        while (true)
        {
            StartCoroutine(BounceDot(_dot1, 0f));
            StartCoroutine(BounceDot(_dot2, _staggerDelay));
            StartCoroutine(BounceDot(_dot3, _staggerDelay * 2f));

            float cycleLength = _bounceDuration + _staggerDelay * 2f + 0.05f;
            yield return new WaitForSeconds(cycleLength);
        }
    }

    private IEnumerator BounceDot(RectTransform dot, float delay)
    {
        if (dot == null) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float half    = _bounceDuration * 0.5f;
        float elapsed = 0f;

        // Rise
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            SetDotY(dot, Mathf.Sin(t * Mathf.PI) * _bounceHeight);
            yield return null;
        }

        SetDotY(dot, 0f);
    }

    private void ResetDots()
    {
        SetDotY(_dot1, 0f);
        SetDotY(_dot2, 0f);
        SetDotY(_dot3, 0f);
    }

    private static void SetDotY(RectTransform dot, float y)
    {
        if (dot == null) return;
        Vector2 pos = dot.anchoredPosition;
        pos.y = y;
        dot.anchoredPosition = pos;
    }
}
