using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using AIRA.MiniGames.SpaceShooter;

namespace AIRA.MiniGames.SpaceShooter{

public class HUD : MonoBehaviour
{
    private UIDocument m_uiDocument;
    private VisualElement m_gameOverScreen;

    [Header("Manager References")]
    [SerializeField] private ScoreManager m_scoreManager;

    [Header("UI Sound Settings")]
    [SerializeField] private SoundEffectHandler m_buttonClickSound;
    [SerializeField] private SoundEffectHandler m_buttonHoverSound;

    private void OnEnable()
    {
        // 1. Inisialisasi UI Document
        m_uiDocument = GetComponent<UIDocument>();
        if (m_uiDocument == null) return;

        VisualElement root = m_uiDocument.rootVisualElement;

        // 2. Setup Data Binding untuk TopBar (Score & Lives)
        VisualElement topBar = root.Q<VisualElement>("TopBar");
        if (topBar != null && m_scoreManager != null)
        {
            topBar.dataSource = m_scoreManager;
        }

        // 3. Setup Game Over Screen & Button Callbacks
        m_gameOverScreen = root.Q<VisualElement>("GameOver");

        if (m_gameOverScreen != null)
        {
            // Ambil Tombol Retry
            VisualElement retryButton = m_gameOverScreen.Q<VisualElement>("Retry");
            if (retryButton != null)
            {
                retryButton.AddManipulator(new Clickable(HandleRetryEvent));
            }

            // Ambil Tombol Menu
            VisualElement menuButton = m_gameOverScreen.Q<VisualElement>("Exit");
            if (menuButton != null)
            {
                menuButton.RegisterCallback<ClickEvent>(ev => OnMenuClicked());
            }
        }

        // 4. Otomatis Tambahkan Suara ke Semua Button di UI
        root.Query<Button>().ForEach(button => 
        {
            button.RegisterCallback<MouseEnterEvent>(evt => PlayHoverSound());
            button.RegisterCallback<ClickEvent>(evt => PlayClickSound());
        });

        // 5. Berlangganan ke Event Global
        if (GameEvents.Instance != null)
        {
            GameEvents.Instance.onGameOver += OnGameOver;
        }
    }

    private void PlayHoverSound()
    {
        if (m_buttonHoverSound != null) m_buttonHoverSound.Play();
    }

    private void PlayClickSound()
    {
        if (m_buttonClickSound != null) m_buttonClickSound.Play();
    }

    private void OnDisable()
    {
        if (GameEvents.Instance != null)
        {
            GameEvents.Instance.onGameOver -= OnGameOver;
        }
    }

    private void OnGameOver()
    {
        if (m_gameOverScreen != null)
        {
            m_gameOverScreen.RemoveFromClassList("hidden");
            Debug.Log("HUD: Menampilkan Layar Game Over");
        }
    }

    private void HandleRetryEvent()
    {
        Debug.Log("HUD: Restarting Game...");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Kembali ke main menu
    private void OnMenuClicked()
    {
        GameManager.Instance?.EndSpaceShooter();
    }
}
}