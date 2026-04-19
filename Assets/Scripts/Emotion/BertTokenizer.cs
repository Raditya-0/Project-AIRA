using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AIRA.Emotion
{
    // Tokenizer WordPiece untuk DistilBERT
    public class BertTokenizer
    {
        private Dictionary<string, int> _vocab;
        private int _clsId, _sepId, _padId, _unkId;
        private int _maxLength;

        // Load vocab dari path
        public BertTokenizer(string vocabPath, int maxLength = 128)
        {
            _maxLength = maxLength;
            _vocab     = new Dictionary<string, int>(StringComparer.Ordinal);

            string[] lines = File.ReadAllLines(vocabPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
                _vocab[lines[i]] = i;

            _clsId = _vocab.TryGetValue("[CLS]", out int cls) ? cls : 101;
            _sepId = _vocab.TryGetValue("[SEP]", out int sep) ? sep : 102;
            _padId = _vocab.TryGetValue("[PAD]", out int pad) ? pad : 0;
            _unkId = _vocab.TryGetValue("[UNK]", out int unk) ? unk : 100;
        }

        // Tokenize teks → input_ids + attention_mask
        public (int[] inputIds, int[] attentionMask) Tokenize(string text)
        {
            List<string> tokens    = WordPieceTokenize(text.ToLowerInvariant().Trim());
            int          maxTokens = _maxLength - 2;

            if (tokens.Count > maxTokens)
                tokens = tokens.GetRange(0, maxTokens);

            int[] inputIds      = new int[_maxLength];
            int[] attentionMask = new int[_maxLength];

            inputIds[0]      = _clsId;
            attentionMask[0] = 1;

            for (int i = 0; i < tokens.Count; i++)
            {
                inputIds[i + 1]      = _vocab.TryGetValue(tokens[i], out int id) ? id : _unkId;
                attentionMask[i + 1] = 1;
            }

            int sepIdx            = tokens.Count + 1;
            inputIds[sepIdx]      = _sepId;
            attentionMask[sepIdx] = 1;

            // Sisa diisi padding (0)
            for (int i = sepIdx + 1; i < _maxLength; i++)
            {
                inputIds[i]      = _padId;
                attentionMask[i] = 0;
            }

            return (inputIds, attentionMask);
        }

        // WordPiece subword tokenization
        private List<string> WordPieceTokenize(string text)
        {
            List<string> result     = new List<string>();
            List<string> rawTokens = SplitOnPunctuation(text);

            foreach (string word in rawTokens)
            {
                if (string.IsNullOrEmpty(word)) continue;

                // Coba match seluruh kata dulu
                if (_vocab.ContainsKey(word))
                {
                    result.Add(word);
                    continue;
                }

                // Pecah jadi subword
                bool isBad     = false;
                int  start     = 0;
                List<string> subTokens = new List<string>();

                while (start < word.Length)
                {
                    int    end    = word.Length;
                    string curStr = null;

                    while (start < end)
                    {
                        string substr = word.Substring(start, end - start);
                        if (start > 0) substr = "##" + substr;

                        if (_vocab.ContainsKey(substr))
                        {
                            curStr = substr;
                            break;
                        }
                        end--;
                    }

                    if (curStr == null)
                    {
                        isBad = true;
                        break;
                    }

                    subTokens.Add(curStr);
                    start = end;
                }

                if (isBad)
                    result.Add("[UNK]");
                else
                    result.AddRange(subTokens);
            }

            return result;
        }

        // Split spasi dan tanda baca
        private List<string> SplitOnPunctuation(string text)
        {
            List<string> tokens  = new List<string>();
            StringBuilder current = new StringBuilder();

            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else if (char.IsPunctuation(c) || char.IsSymbol(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                    tokens.Add(c.ToString());
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }
    }
}
