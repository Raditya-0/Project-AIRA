using System.IO;
using UnityEngine;

namespace AIRA.Core
{
    // Pusat konfigurasi path model
    public static class PathConfig
    {
        private const string k_PiperDir   = "piper";
        private const string k_VoicesDir  = "voices";
        private const string k_PiperExe   = "piper.exe";
        private const string k_PiperVoice = "en_US-amy-medium.onnx";
        private const string k_LLMModel   = "qwen2.5-3b-q4.gguf";
        private const string k_STTModel   = "ggml-small.en-q5_1.bin";
        private const string k_EmotionDir = "EmotionClassifier";
        private const string k_Vocab      = "vocab.txt";
        private const string k_Labels     = "emotion_labels.json";

        // Path executable Piper TTS
        public static string PiperExe =>
            Path.Combine(Application.streamingAssetsPath, k_PiperDir, k_PiperExe);

        // Path model suara Piper
        public static string PiperVoice =>
            Path.Combine(Application.streamingAssetsPath, k_PiperDir, k_VoicesDir, k_PiperVoice);

        // Path model LLM gguf
        public static string LLMModel =>
            Path.Combine(Application.streamingAssetsPath, k_LLMModel);

        // Path model Whisper STT
        public static string WhisperModel =>
            Path.Combine(Application.streamingAssetsPath, k_STTModel);

        // Path vocab tokenizer emosi
        public static string EmotionVocab =>
            Path.Combine(Application.streamingAssetsPath, k_EmotionDir, k_Vocab);

        // Path label klasifikasi emosi
        public static string EmotionLabels =>
            Path.Combine(Application.streamingAssetsPath, k_EmotionDir, k_Labels);

        // Validasi semua path model
        public static void Validate()
        {
            Check(PiperExe,      "Piper exe");
            Check(PiperVoice,    "Piper voice");
            Check(LLMModel,      "LLM model");
            Check(WhisperModel,  "Whisper model");
            Check(EmotionVocab,  "Emotion vocab");
            Check(EmotionLabels, "Emotion labels");
        }

        // Warn jika file tidak ada
        private static void Check(string path, string label)
        {
            if (!File.Exists(path))
                Debug.LogWarning($"[PathConfig] {label} tidak ditemukan: {path}");
        }
    }
}
