using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIRA.UI;
using AIRA.Voice;

namespace AIRA.Character
{
    // Zona tubuh Aira
    public enum AiraBodyZone { Head, Body, Hand }

    // Data reaksi per zona
    [System.Serializable]
    public class ZoneReaction
    {
        public AiraBodyZone zone;
        public string       expression;

        [TextArea(2, 4)]
        public string[] reactionLines;
    }

    // Koordinasi interaksi klik karakter
    public class AiraClickInteraction : MonoBehaviour
    {
        [Header("Reaksi Per Zona")]
        [SerializeField] private List<ZoneReaction> _zoneReactions = new List<ZoneReaction>
        {
            new ZoneReaction
            {
                zone          = AiraBodyZone.Head,
                expression    = "[SHY]",
                reactionLines = new[]
                {
                    "Hey, don't poke me like that!",
                    "W-what are you doing to my head...",
                    "Stop it, that tickles!"
                }
            },
            new ZoneReaction
            {
                zone          = AiraBodyZone.Body,
                expression    = "[SURPRISED]",
                reactionLines = new[]
                {
                    "Wah, personal space please!",
                    "H-hey! That's too close!",
                    "Excuse me?! What was that for?"
                }
            },
            new ZoneReaction
            {
                zone          = AiraBodyZone.Hand,
                expression    = "[HAPPY]",
                reactionLines = new[]
                {
                    "Are you holding my hand? Hehe.",
                    "Oh, hi there!",
                    "You want a high five? Here!"
                }
            }
        };

        [Header("Cooldown")]
        [SerializeField] private float _clickCooldown = 1.5f;

        private bool _isOnCooldown;
        private Dictionary<AiraBodyZone, ZoneReaction> _reactionMap;

        // Inisialisasi reaction map
        private void Awake() => BuildReactionMap();

        // Bangun dictionary reaksi
        private void BuildReactionMap()
        {
            _reactionMap = new Dictionary<AiraBodyZone, ZoneReaction>();
            foreach (var r in _zoneReactions)
                if (!_reactionMap.ContainsKey(r.zone))
                    _reactionMap[r.zone] = r;
        }

        // Terima klik dari AiraClickZone
        public void OnZoneClicked(AiraBodyZone zone)
        {
            if (_isOnCooldown) return;
            if (GameManager.Instance == null) return;

            var state = GameManager.Instance.CurrentState;
            if (state != GameManager.GameState.IDLE && state != GameManager.GameState.LISTENING) return;

            if (!_reactionMap.TryGetValue(zone, out ZoneReaction reaction)) return;
            StartCoroutine(PlayReaction(reaction));
        }

        // Mainkan reaksi Aira
        private IEnumerator PlayReaction(ZoneReaction reaction)
        {
            _isOnCooldown = true;

            string line       = PickRandomLine(reaction.reactionLines);
            string expression = reaction.expression.Trim('[', ']');

            AiraController.Instance?.SetExpression(reaction.expression);
            ChatUIManager.Instance?.DisplayMessage("aira", line);
            FindFirstObjectByType<AiraFloatingBubble>()?.ShowDialogBubble(line, 3f);

            GameManager.Instance?.ChangeState(GameManager.GameState.SPEAKING);

            if (TTSManager.Instance != null)
            {
                TTSManager.Instance.Speak(line, expression);
                yield return new WaitUntil(() =>
                    GameManager.Instance == null ||
                    GameManager.Instance.CurrentState != GameManager.GameState.SPEAKING);
            }
            else
            {
                yield return new WaitForSeconds(3f);
                GameManager.Instance?.ChangeState(GameManager.GameState.IDLE);
            }

            yield return new WaitForSeconds(_clickCooldown);
            _isOnCooldown = false;
        }

        // Pilih baris acak
        private string PickRandomLine(string[] lines)
        {
            if (lines == null || lines.Length == 0) return string.Empty;
            return lines[Random.Range(0, lines.Length)];
        }
    }
}
