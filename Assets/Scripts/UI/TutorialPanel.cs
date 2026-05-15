using UnityEngine;
using UnityEngine.UI;

namespace AIRA.UI
{
    public class TutorialPanel : MonoBehaviour
    {
        public static TutorialPanel Instance { get; private set; }

        // Status panel tutorial terbuka
        public bool IsTutorialOpen => _tutorialPanel != null && _tutorialPanel.activeSelf;

        [Header("Panel")]
        [SerializeField] private GameObject _tutorialPanel;
        [SerializeField] private Button     _closeButton;

        [Header("Settings")]
        [SerializeField] private string _tutorialKey  = "Tutorial_Shown";
        [SerializeField] private bool   _showOnlyOnce = true;

        [Header("Canvas")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private int    _defaultSortOrder  = 0;
        [SerializeField] private int    _prioritySortOrder = 100;

        // Inisialisasi singleton
        private void Awake()
        {
            Instance = this;
        }

        // Tampilkan atau skip tutorial
        private void Start()
        {
            bool alreadyShown = _showOnlyOnce && PlayerPrefs.GetInt(_tutorialKey, 0) == 1;

            if (alreadyShown)
            {
                _tutorialPanel?.SetActive(false);
                OnTutorialClosed();
                return;
            }

            _tutorialPanel?.SetActive(true);
            Time.timeScale = 0f;
            _closeButton?.onClick.AddListener(CloseTutorial);
        }

        // Hapus singleton dan listener
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _closeButton?.onClick.RemoveListener(CloseTutorial);
        }

        // Buka dari settings dengan prioritas tinggi
        public void ShowFromSettings()
        {
            if (_canvas != null)
                _canvas.sortingOrder = _prioritySortOrder;

            _tutorialPanel?.SetActive(true);
            Time.timeScale = 0f;
            _closeButton?.onClick.AddListener(CloseTutorial);
        }

        // Tutup panel dan mulai game
        public void CloseTutorial()
        {
            if (_canvas != null)
                _canvas.sortingOrder = _defaultSortOrder;

            if (_showOnlyOnce)
                PlayerPrefs.SetInt(_tutorialKey, 1);

            _tutorialPanel?.SetActive(false);
            Time.timeScale = 1f;
            OnTutorialClosed();
        }

        // Reset agar tutorial muncul lagi
        public void ResetTutorial()
        {
            PlayerPrefs.DeleteKey(_tutorialKey);
        }

        // Hook untuk subclass atau extension
        protected virtual void OnTutorialClosed() { }
    }
}
