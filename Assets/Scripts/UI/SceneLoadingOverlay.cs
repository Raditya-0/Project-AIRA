using System.Collections;
using UnityEngine;

public class SceneLoadingOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LoadingScreenUI _loadingScreen;

    [Header("Per-Scene Config")]
    [SerializeField] private Sprite   _backgroundSprite;
    [SerializeField] private string[] _hints;
    [SerializeField] private float    _minDisplayTime = 1f;

    // Tampilkan overlay lalu sembunyikan
    private void Start()
    {
        if (_loadingScreen == null) return;
        _loadingScreen.gameObject.SetActive(true);
        _loadingScreen.SetBackground(_backgroundSprite);
        _loadingScreen.SetHints(_hints);
        StartCoroutine(HideAfterDelay());
    }

    // Tunggu lalu trigger fade out
    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(_minDisplayTime);
        _loadingScreen.SetProgress(1f, "");
    }
}
