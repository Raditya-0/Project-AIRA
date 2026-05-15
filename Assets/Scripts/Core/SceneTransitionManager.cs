using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [SerializeField] private LoadingScreenUI _loadingScreenPrefab;

    private LoadingScreenUI _currentOverlay;

    // Inisialisasi singleton persisten
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Muat scene dengan overlay
    public void LoadScene(string sceneName, Sprite background = null, string[] hints = null)
    {
        StartCoroutine(LoadSceneAsync(sceneName, background, hints));
    }

    // Async load dengan progress overlay
    private IEnumerator LoadSceneAsync(string sceneName, Sprite background, string[] hints)
    {
        if (_loadingScreenPrefab != null)
        {
            _currentOverlay = Instantiate(_loadingScreenPrefab);
            DontDestroyOnLoad(_currentOverlay.gameObject);
            _currentOverlay.SetBackground(background);
            if (hints != null) _currentOverlay.SetHints(hints);
            _currentOverlay.SetProgress(0f, "Loading...");
        }

        yield return new WaitForSecondsRealtime(0.1f);

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            _currentOverlay?.SetProgress(op.progress, "Loading...");
            yield return null;
        }

        _currentOverlay?.SetProgress(1f, "");
        yield return new WaitForSecondsRealtime(0.5f);
        op.allowSceneActivation = true;
    }
}
