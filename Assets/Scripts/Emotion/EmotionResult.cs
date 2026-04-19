using System;
using System.Collections.Generic;

namespace AIRA.Emotion
{
    // Data hasil klasifikasi emosi
    [Serializable]
    public class EmotionResult
    {
        public string dominantEmotion;
        public float  confidence;
        public string airaHint;
        public List<EmotionScore> top3;

        // Format string untuk LLM context
        public string ToLLMContext()
        {
            string secondary = top3 != null && top3.Count > 1
                ? $"{top3[1].emotion} ({top3[1].confidence:P0})"
                : "-";

            return
                $"[PLAYER EMOTION DETECTED]\n" +
                $"Dominant: {dominantEmotion} ({confidence:P0} confidence)\n" +
                $"Secondary: {secondary}\n" +
                $"Suggested tone: {airaHint}";
        }
    }

    // Satu skor per emosi
    [Serializable]
    public class EmotionScore
    {
        public string emotion;
        public float  confidence;
    }

    // Metadata dari JSON label
    [Serializable]
    public class EmotionMetadata
    {
        public List<string>               labels;
        public Dictionary<string, string> aira_hints;
    }
}
