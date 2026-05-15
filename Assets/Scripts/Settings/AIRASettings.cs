using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using AIRA.AI;
using AIRA.Voice;

namespace AIRA.Core
{
    public class AIRASettings : MonoBehaviour
    {
        // Singleton instance global
        public static AIRASettings Instance { get; private set; }

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer _audioMixer;

        [Header("Volume")]
        [SerializeField] [Range(0f, 1f)] private float _masterVolume = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float _musicVolume  = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float _sfxVolume    = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float _ttsVolume    = 0.5f;

        [Header("Device")]
        [SerializeField] private int _microphoneDeviceIndex = 0;

        [Header("AI & Memory")]
        [SerializeField] private bool _useEmotionClassifier = false;

        [Header("Voice")]
        [SerializeField] private bool _ttsEnabled = true;
        [SerializeField] private bool _sttEnabled = true;

        [Header("Platformer Settings")]
        [SerializeField] private bool _platformerSTTEnabled = false;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLog = true;

        // Properti akses dari luar
        public float MasterVolume          => _masterVolume;
        public float MusicVolume           => _musicVolume;
        public float SFXVolume             => _sfxVolume;
        public float TTSVolume             => _ttsVolume;
        public int   MicrophoneDeviceIndex => _microphoneDeviceIndex;
        public bool  UseEmotionClassifier  => _useEmotionClassifier;
        public bool  TTSEnabled            => _ttsEnabled;
        public bool  STTEnabled            => _sttEnabled;
        public bool  PlatformerSTTEnabled  => _platformerSTTEnabled;
        public bool  ShowDebugLog          => _showDebugLog;

        // Properti konversi untuk UI slider 0-100
        public float MasterVolumeUI => _masterVolume * 100f;
        public float MusicVolumeUI  => _musicVolume  * 100f;
        public float SFXVolumeUI    => _sfxVolume    * 100f;
        public float TTSVolumeUI    => _ttsVolume    * 100f;

        // Inisialisasi singleton dan load settings
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        // Terapkan settings saat mulai
        private void Start()
        {
            ApplyAllSettings();
        }

        // Apply semua settings ke system
        [ContextMenu("Apply Settings")]
        public void ApplyAllSettings()
        {
            AudioListener.volume = _masterVolume;
            ApplyMixerVolume("BGMVolume", _musicVolume);
            ApplyMixerVolume("SFXVolume", _sfxVolume);
            TTSManager.Instance?.SetVolume(_ttsVolume);
            STTManager.Instance?.SetDevice(_microphoneDeviceIndex);
            if (!_sttEnabled) STTManager.Instance?.StopListening();

            if (_showDebugLog)
                Debug.Log("[AIRASettings] Settings applied.");
        }

        // Reset dan hapus file settings
        [ContextMenu("Reset Settings")]
        public void ResetSettings()
        {
            string path = Path.Combine(Application.persistentDataPath, "aira_settings.json");
            if (File.Exists(path)) File.Delete(path);

            _masterVolume          = 0.5f;
            _musicVolume           = 0.5f;
            _sfxVolume             = 0.5f;
            _ttsVolume             = 0.5f;
            _microphoneDeviceIndex = 0;
            _sttEnabled            = true;
            _ttsEnabled            = true;
            _useEmotionClassifier  = false;

            ApplyAllSettings();
            Debug.Log("[AIRASettings] Settings di-reset.");
        }

        // Set dan apply master volume
        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = _masterVolume;
            SaveSettings();
        }

        // Set dan apply music volume
        public void SetMusicVolume(float value)
        {
            _musicVolume = Mathf.Clamp01(value);
            ApplyMixerVolume("BGMVolume", _musicVolume);
            SaveSettings();
        }

        // Set dan apply SFX volume
        public void SetSFXVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);
            ApplyMixerVolume("SFXVolume", _sfxVolume);
            SaveSettings();
        }

        // Set dan apply TTS volume
        public void SetTTSVolume(float value)
        {
            _ttsVolume = Mathf.Clamp01(value);
            TTSManager.Instance?.SetVolume(_ttsVolume);
            SaveSettings();
        }

        // Set microphone device
        public void SetMicrophoneDevice(int index)
        {
            _microphoneDeviceIndex = index;
            STTManager.Instance?.SetDevice(index);
            SaveSettings();
        }

        // Toggle STT on/off
        public void SetSTTEnabled(bool value)
        {
            _sttEnabled = value;
            if (!value) STTManager.Instance?.StopListening();
            else        STTManager.Instance?.StartListening();
            SaveSettings();
        }

        // Toggle TTS on/off
        public void SetTTSEnabled(bool value)
        {
            _ttsEnabled = value;
            SaveSettings();
        }

        // Toggle emotion classifier
        public void SetEmotionClassifier(bool value)
        {
            _useEmotionClassifier = value;
            SaveSettings();
        }

        // Hapus semua memory session
        public void ClearMemory()
        {
            MemoryManager.Instance?.ClearAllMemory();
            Debug.Log("[AIRASettings] Memory cleared.");
        }

        // Konversi linear ke dB dan apply ke mixer
        private void ApplyMixerVolume(string param, float linear)
        {
            if (_audioMixer == null) return;
            float dB = Mathf.Log10(Mathf.Clamp(linear, 0.0001f, 1f)) * 20f;
            _audioMixer.SetFloat(param, dB);
        }

        [Serializable]
        private class SettingsData
        {
            public float masterVolume          = 0.5f;
            public float musicVolume           = 0.5f;
            public float sfxVolume             = 0.5f;
            public float ttsVolume             = 0.5f;
            public int   microphoneDeviceIndex = 0;
            public bool  sttEnabled            = true;
            public bool  ttsEnabled            = true;
            public bool  useEmotionClassifier  = false;
        }

        // Simpan settings ke file
        private void SaveSettings()
        {
            var data = new SettingsData
            {
                masterVolume          = _masterVolume,
                musicVolume           = _musicVolume,
                sfxVolume             = _sfxVolume,
                ttsVolume             = _ttsVolume,
                microphoneDeviceIndex = _microphoneDeviceIndex,
                sttEnabled            = _sttEnabled,
                ttsEnabled            = _ttsEnabled,
                useEmotionClassifier  = _useEmotionClassifier
            };
            string path = Path.Combine(Application.persistentDataPath, "aira_settings.json");
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        // Load settings dari file
        public void LoadSettings()
        {
            string path = Path.Combine(Application.persistentDataPath, "aira_settings.json");
            if (!File.Exists(path)) return;
            try
            {
                var data               = JsonUtility.FromJson<SettingsData>(File.ReadAllText(path));
                _masterVolume          = Mathf.Clamp(data.masterVolume, 0f, 1f);
                _musicVolume           = Mathf.Clamp(data.musicVolume,  0f, 1f);
                _sfxVolume             = Mathf.Clamp(data.sfxVolume,    0f, 1f);
                _ttsVolume             = Mathf.Clamp(data.ttsVolume,    0f, 1f);
                _microphoneDeviceIndex = data.microphoneDeviceIndex;
                _sttEnabled            = data.sttEnabled;
                _ttsEnabled            = data.ttsEnabled;
                _useEmotionClassifier  = data.useEmotionClassifier;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIRASettings] Gagal load settings: {e.Message}");
            }
        }
    }
}
