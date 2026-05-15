using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using Unity.InferenceEngine;
using AIRA.Core;

namespace AIRA.Emotion
{
    // Singleton classifier emosi berbasis ONNX
    public class EmotionClassifier : MonoBehaviour
    {
        [Header("Model Settings")]
        [SerializeField] private ModelAsset _modelAsset;
        [SerializeField] private int        _maxLength = 128;

        [Header("Runtime Settings")]
        [SerializeField] private float _confidenceThreshold = 0.35f;
        [SerializeField] private bool  _logPredictions      = true;

        // Singleton global EmotionClassifier
        public static EmotionClassifier Instance { get; private set; }

        // Status apakah sudah siap
        public bool IsReady { get; private set; }

        private Model           _runtimeModel;
        private Worker          _worker;
        private BertTokenizer   _tokenizer;
        private EmotionMetadata _metadata;

        // Inisialisasi singleton Awake
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // Mulai load aset async
        private IEnumerator Start()
        {
            yield return StartCoroutine(LoadModelAsync());
        }

        // Load model vocab dan labels
        private IEnumerator LoadModelAsync()
        {
            if (_modelAsset == null)
            {
                Debug.LogError("[EmotionClassifier] ModelAsset belum di-assign di Inspector.");
                yield break;
            }

            _runtimeModel = ModelLoader.Load(_modelAsset);
            _worker       = new Worker(_runtimeModel, BackendType.GPUCompute);
            Debug.Log("[EmotionClassifier] Model ONNX berhasil dimuat.");

            // Load vocab tokenizer
            string vocabPath = PathConfig.EmotionVocab;
            if (!File.Exists(vocabPath))
            {
                Debug.LogWarning($"[EmotionClassifier] Vocab tidak ditemukan: {vocabPath}");
                yield break;
            }
            _tokenizer = new BertTokenizer(vocabPath, _maxLength);

            // Load label metadata
            string labelsPath = PathConfig.EmotionLabels;
            if (!File.Exists(labelsPath))
            {
                Debug.LogWarning($"[EmotionClassifier] Labels tidak ditemukan: {labelsPath}");
                yield break;
            }
            string json = File.ReadAllText(labelsPath);
            _metadata   = JsonConvert.DeserializeObject<EmotionMetadata>(json);
            Debug.Log($"[EmotionClassifier] {_metadata.labels.Count} label dimuat.");

            IsReady = true;
            Debug.Log("[EmotionClassifier] Siap digunakan.");
            LoadingGate.Instance?.SetEmotionReady();
        }

        // Public API klasifikasi dengan callback
        public void Classify(string text, Action<EmotionResult> onComplete)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(text))
            {
                onComplete?.Invoke(GetNeutralResult());
                return;
            }
            StartCoroutine(ClassifyCoroutine(text, onComplete));
        }

        // Coroutine inference Sentis 2.5
        private IEnumerator ClassifyCoroutine(string text, Action<EmotionResult> onComplete)
        {
            var (inputIds, attentionMask) = _tokenizer.Tokenize(text);

            using var inputIdsTensor      = new Tensor<int>(new TensorShape(1, _maxLength), inputIds);
            using var attentionMaskTensor = new Tensor<int>(new TensorShape(1, _maxLength), attentionMask);

            _worker.SetInput("input_ids",      inputIdsTensor);
            _worker.SetInput("attention_mask", attentionMaskTensor);
            _worker.Schedule();

            yield return null;

            Tensor<float> logitsTensor = _worker.PeekOutput("logits") as Tensor<float>;
            if (logitsTensor == null)
            {
                Debug.LogWarning("[EmotionClassifier] Output logits null — fallback neutral.");
                onComplete?.Invoke(GetNeutralResult());
                yield break;
            }

            float[] logits = logitsTensor.DownloadToArray();
            float[] probs  = Softmax(logits);

            EmotionResult result = BuildResult(probs);

            if (_logPredictions)
                Debug.Log($"[EmotionClassifier] {result.dominantEmotion} ({result.confidence:P0}) — {result.airaHint}");

            onComplete?.Invoke(result);
        }

        // Bangun result dari probabilities
        private EmotionResult BuildResult(float[] probs)
        {
            int labelCount = _metadata?.labels?.Count ?? probs.Length;

            var scored = new List<EmotionScore>(labelCount);
            for (int i = 0; i < labelCount && i < probs.Length; i++)
            {
                scored.Add(new EmotionScore
                {
                    emotion    = _metadata.labels[i],
                    confidence = probs[i]
                });
            }

            scored.Sort((a, b) => b.confidence.CompareTo(a.confidence));

            EmotionScore dominant = scored[0];

            // Fallback neutral bila confidence rendah
            if (dominant.confidence < _confidenceThreshold)
                return GetNeutralResult();

            _metadata.aira_hints.TryGetValue(dominant.emotion, out string hint);

            return new EmotionResult
            {
                dominantEmotion = dominant.emotion,
                confidence      = dominant.confidence,
                airaHint        = hint ?? "neutral",
                top3            = scored.GetRange(0, Math.Min(3, scored.Count))
            };
        }

        // Softmax numerically stable
        private float[] Softmax(float[] logits)
        {
            float max = logits[0];
            for (int i = 1; i < logits.Length; i++)
                if (logits[i] > max) max = logits[i];

            float[] exps = new float[logits.Length];
            float   sum  = 0f;
            for (int i = 0; i < logits.Length; i++)
            {
                exps[i] = Mathf.Exp(logits[i] - max);
                sum     += exps[i];
            }
            for (int i = 0; i < exps.Length; i++)
                exps[i] /= sum;

            return exps;
        }

        // Fallback result emosi neutral
        private EmotionResult GetNeutralResult()
        {
            string hint = "neutral";
            _metadata?.aira_hints?.TryGetValue("neutral", out hint);

            return new EmotionResult
            {
                dominantEmotion = "neutral",
                confidence      = 1f,
                airaHint        = hint ?? "neutral",
                top3            = new List<EmotionScore>
                {
                    new EmotionScore { emotion = "neutral", confidence = 1f }
                }
            };
        }

        // Dispose Sentis worker
        private void OnDestroy()
        {
            _worker?.Dispose();
            _runtimeModel = null;
        }
    }
}
