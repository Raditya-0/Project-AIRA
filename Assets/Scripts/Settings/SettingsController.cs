using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using AIRA.Core;
using AIRA.Voice;

namespace AIRA.UI
{
    public class SettingsController : MonoBehaviour
    {
        [Header("Sliders")]
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Slider _ttsVoiceSlider;

        [Header("Nilai Slider")]
        [SerializeField] private TMP_Text _masterValue;
        [SerializeField] private TMP_Text _musicValue;
        [SerializeField] private TMP_Text _sfxValue;
        [SerializeField] private TMP_Text _ttsVoiceValue;

        [Header("Toggles")]
        [SerializeField] private CustomToggle _sttToggle;
        [SerializeField] private CustomToggle _ttsToggle;
        [SerializeField] private CustomToggle _emotionToggle;

        [Header("Lainnya")]
        [SerializeField] private TMP_Dropdown _micDropdown;
        [SerializeField] private Button _clearMemoryBtn;
        [SerializeField] protected Button _closeBtn;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip   _closeSound;

        private bool _isLoading;
        private Coroutine _populateCoroutine;

        protected virtual void OnEnable()
        {
            LoadCurrentSettings();
            RegisterSemuaCallback();

            if (STTManager.Instance != null && STTManager.Instance.IsInitialized)
                PopulateDropdown();
            else
                _populateCoroutine = StartCoroutine(WaitAndPopulateDropdown());
        }

        protected virtual void OnDisable()
        {
            if (_populateCoroutine != null)
            {
                StopCoroutine(_populateCoroutine);
                _populateCoroutine = null;
            }
            UnregisterSemuaCallback();
        }

        private void LoadCurrentSettings()
        {
            _isLoading = true;

            float master = AIRASettings.Instance.MasterVolume * 100f;
            float music  = AIRASettings.Instance.MusicVolume  * 100f;
            float sfx    = AIRASettings.Instance.SFXVolume    * 100f;
            float tts    = AIRASettings.Instance.TTSVolume    * 100f;

            _masterSlider.SetValueWithoutNotify(master);
            _musicSlider.SetValueWithoutNotify(music);
            _sfxSlider.SetValueWithoutNotify(sfx);
            _ttsVoiceSlider.SetValueWithoutNotify(tts);

            _masterValue.text   = Mathf.RoundToInt(master).ToString();
            _musicValue.text    = Mathf.RoundToInt(music).ToString();
            _sfxValue.text      = Mathf.RoundToInt(sfx).ToString();
            _ttsVoiceValue.text = Mathf.RoundToInt(tts).ToString();

            _sttToggle.SetState(AIRASettings.Instance.STTEnabled);
            _ttsToggle.SetState(AIRASettings.Instance.TTSEnabled);
            _emotionToggle.SetState(AIRASettings.Instance.UseEmotionClassifier);

            _isLoading = false;
        }

        private void PopulateDropdown()
        {
            string[] devices = STTManager.Instance?.GetAvailableDevices() ?? new string[0];
            var list = new List<string>(devices);
            if (list.Count == 0) list.Add("No device found");

            _micDropdown.ClearOptions();
            _micDropdown.AddOptions(list);
            
            int targetIndex = Mathf.Clamp(AIRASettings.Instance.MicrophoneDeviceIndex, 0, list.Count - 1);
            _micDropdown.SetValueWithoutNotify(targetIndex);
        }

        private IEnumerator WaitAndPopulateDropdown()
        {
            yield return new WaitUntil(() => STTManager.Instance != null && STTManager.Instance.IsInitialized);
            PopulateDropdown();
            _populateCoroutine = null;
        }

        private void RegisterSemuaCallback()
        {
            _masterSlider.onValueChanged.AddListener(OnMasterChanged);
            _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            _sfxSlider.onValueChanged.AddListener(OnSFXChanged);
            _ttsVoiceSlider.onValueChanged.AddListener(OnTTSVoiceChanged);
            _micDropdown.onValueChanged.AddListener(OnMicrophoneChanged);
            _clearMemoryBtn.onClick.AddListener(OnClearMemory);
            _closeBtn.onClick.AddListener(OnClose);
            _sttToggle.OnValueChanged     += OnSTTChanged;
            _ttsToggle.OnValueChanged     += OnTTSChanged;
            _emotionToggle.OnValueChanged += OnEmotionChanged;
        }

        private void UnregisterSemuaCallback()
        {
            _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            _sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
            _ttsVoiceSlider.onValueChanged.RemoveListener(OnTTSVoiceChanged);
            _micDropdown.onValueChanged.RemoveListener(OnMicrophoneChanged);
            _clearMemoryBtn.onClick.RemoveListener(OnClearMemory);
            _closeBtn.onClick.RemoveListener(OnClose);
            _sttToggle.OnValueChanged     -= OnSTTChanged;
            _ttsToggle.OnValueChanged     -= OnTTSChanged;
            _emotionToggle.OnValueChanged -= OnEmotionChanged;
        }

        private void OnMasterChanged(float value)
        {
            if (_isLoading) return;
            _masterValue.text = Mathf.RoundToInt(value).ToString();
            AIRASettings.Instance?.SetMasterVolume(value / 100f);
        }

        private void OnMusicChanged(float value)
        {
            if (_isLoading) return;
            _musicValue.text = Mathf.RoundToInt(value).ToString();
            AIRASettings.Instance?.SetMusicVolume(value / 100f);
        }

        private void OnSFXChanged(float value)
        {
            if (_isLoading) return;
            _sfxValue.text = Mathf.RoundToInt(value).ToString();
            AIRASettings.Instance?.SetSFXVolume(value / 100f);
        }

        private void OnTTSVoiceChanged(float value)
        {
            if (_isLoading) return;
            _ttsVoiceValue.text = Mathf.RoundToInt(value).ToString();
            AIRASettings.Instance?.SetTTSVolume(value / 100f);
        }

        private void OnSTTChanged(bool value)
        {
            if (_isLoading) return;
            AIRASettings.Instance?.SetSTTEnabled(value);
        }

        private void OnTTSChanged(bool value)
        {
            if (_isLoading) return;
            AIRASettings.Instance?.SetTTSEnabled(value);
        }

        private void OnEmotionChanged(bool value)
        {
            if (_isLoading) return;
            AIRASettings.Instance?.SetEmotionClassifier(value);
        }

        private void OnMicrophoneChanged(int index)
        {
            if (_isLoading) return;
            AIRASettings.Instance?.SetMicrophoneDevice(index);
        }

        private void OnClearMemory()
        {
            AIRASettings.Instance?.ClearMemory();
        }

        // Mulai coroutine tutup
        protected virtual void OnClose()
        {
            StartCoroutine(CloseWithSound());
        }

        // Tunggu audio selesai tutup
        private IEnumerator CloseWithSound()
        {
            if (_audioSource != null && _audioSource.gameObject.activeInHierarchy && _audioSource.enabled)
            {
                _audioSource.PlayOneShot(_closeSound);
                yield return new WaitUntil(() => !_audioSource.isPlaying);
            }
            gameObject.SetActive(false);
        }
    }
}