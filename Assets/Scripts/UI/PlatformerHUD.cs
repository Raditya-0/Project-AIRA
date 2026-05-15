using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace AIRA.UI
{
    public class PlatformerHUD : MonoBehaviour
    {
        [Header("Level Display")]
        [SerializeField] private TMP_Text _levelText;

        [Header("Referensi")]
        [SerializeField] private PlatformerPauseManager _pauseManager;

        private void Start()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            string levelLabel = ParseLevelLabel(sceneName);
            if (_levelText != null)
                _levelText.text = levelLabel;
        }

        private string ParseLevelLabel(string sceneName)
        {
            if (sceneName.Length >= 2)
            {
                string lastTwo = sceneName.Substring(sceneName.Length - 2);
                if (int.TryParse(lastTwo, out int num))
                    return $"Level {num}";
            }
            return "Level 1";
        }

        public void OnClickPause()
        {
            _pauseManager?.OpenPause();
        }
    }
}