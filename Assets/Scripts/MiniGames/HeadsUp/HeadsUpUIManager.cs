using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeadsUpUIManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject _gamePanel;

    [Header("Game UI")]
    [SerializeField] private TextMeshProUGUI _wordText;
    [SerializeField] private TextMeshProUGUI _categoryText;
    [SerializeField] private TextMeshProUGUI _timerText;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private Button          _playButton;

    // Daftarkan listener tombol play
    private void Start()
    {
        _playButton?.onClick.AddListener(() => HeadsUpGame.Instance?.StartGame());
        ShowGamePanel(false);
    }

    // Tampilkan kata sekarang
    public void ShowWord(string word)
    {
        if (_wordText != null)
            _wordText.text = word;
    }

    // Update tampilan timer
    public void UpdateTimer(float seconds)
    {
        if (_timerText == null) return;

        int totalSeconds = Mathf.CeilToInt(Mathf.Max(seconds, 0f));
        
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;

        _timerText.text = string.Format("{0}:{1:00}", minutes, remainingSeconds);
    }

    // Update tampilan skor
    public void UpdateScore(int correct, int skip)
    {
        if (_scoreText != null)
            _scoreText.text = $"{correct}";
    }

    // Update label kategori
    public void UpdateCategory(string category)
    {
        if (_categoryText != null)
            _categoryText.text = $"{category}";
    }

    // Toggle panel game saja
    public void ShowGamePanel(bool show)
    {
        if (_gamePanel != null)
            _gamePanel.SetActive(show);
    }
}
