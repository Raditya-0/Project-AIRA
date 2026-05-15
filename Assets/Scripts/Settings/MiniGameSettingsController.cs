using UnityEngine;
using UnityEngine.UI;
using AIRA.UI;

namespace AIRA.UI
{
    public class MiniGameSettingsController : SettingsController
    {
        [Header("Tutorial")]
        [SerializeField] private Button        _showTutorialBtn;
        [SerializeField] private TutorialPanel _tutorialPanel;

        // Daftar listener tutorial
        protected override void OnEnable()
        {
            base.OnEnable();
            _showTutorialBtn?.onClick.AddListener(OnShowTutorial);
        }

        // Hapus listener tutorial
        protected override void OnDisable()
        {
            base.OnDisable();
            _showTutorialBtn?.onClick.RemoveListener(OnShowTutorial);
        }

        // Buka kembali panel tutorial
        private void OnShowTutorial()
        {
            _tutorialPanel?.ResetTutorial();
            gameObject.SetActive(false);
            _tutorialPanel?.ShowFromSettings();
        }
    }
}
