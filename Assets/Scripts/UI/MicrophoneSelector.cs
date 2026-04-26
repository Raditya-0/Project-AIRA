using UnityEngine;
using TMPro;
using AIRA.Voice;

namespace AIRA.UI
{
    public class MicrophoneSelector : MonoBehaviour
    {
        // Referensi dropdown microphone
        [SerializeField] private TMP_Dropdown _dropdown;

        // Start populate daftar mic
        private void Start()
        {
            STTManager.Instance?.PopulateDropdown(_dropdown);
        }
    }
}
