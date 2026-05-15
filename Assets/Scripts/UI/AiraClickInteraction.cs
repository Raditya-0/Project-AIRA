using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AIRA.Character;
using AIRA.Voice;

namespace AIRA.UI
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

    // Interaksi klik via UI Button
    public class AiraClickInteraction : MonoBehaviour
    {
        [Header("Zone Buttons")]
        [SerializeField] private Button _headButton;
        [SerializeField] private Button _bodyButton;
        [SerializeField] private Button _handLeftButton;
        [SerializeField] private Button _handRightButton;

        [Header("Reaksi Per Zona")]
        [SerializeField] private List<ZoneReaction> _zoneReactions = new List<ZoneReaction>
        {
            new ZoneReaction
            {
                zone          = AiraBodyZone.Head,
                expression    = "[SHY]",
                reactionLines = new[] { "Hey, don't poke me like that!", "W-what are you doing..." }
            },
            new ZoneReaction
            {
                zone          = AiraBodyZone.Body,
                expression    = "[SURPRISED]",
                reactionLines = new[] { "Wah, personal space please!", "H-hey! That's too close!" }
            },
            new ZoneReaction
            {
                zone          = AiraBodyZone.Hand,
                expression    = "[HAPPY]",
                reactionLines = new[] { "Are you holding my hand? Hehe.", "Oh, hi there!" }
            }
        };

        [Header("Cooldown")]
        [SerializeField] private float _clickCooldown    = 1.5f;
        [SerializeField] private float _reactionDuration = 3f;

        private bool _isOnCooldown;
        private Dictionary<AiraBodyZone, ZoneReaction> _reactionMap;

        // Inisialisasi reaction map
        private void Awake()
        {
            BuildReactionMap();
        }

        // Daftarkan listener tiap button
        private void Start()
        {
            if (_headButton != null)
                _headButton.onClick.AddListener(() => OnZoneClicked(AiraBodyZone.Head));
            if (_bodyButton != null)
                _bodyButton.onClick.AddListener(() => OnZoneClicked(AiraBodyZone.Body));
            if (_handLeftButton != null)
                _handLeftButton.onClick.AddListener(() => OnZoneClicked(AiraBodyZone.Hand));
            if (_handRightButton != null)
                _handRightButton.onClick.AddListener(() => OnZoneClicked(AiraBodyZone.Hand));
        }

        // Hapus listener saat destroy
        private void OnDestroy()
        {
            _headButton?.onClick.RemoveAllListeners();
            _bodyButton?.onClick.RemoveAllListeners();
            _handLeftButton?.onClick.RemoveAllListeners();
            _handRightButton?.onClick.RemoveAllListeners();
        }

        // Bangun dictionary reaksi
        private void BuildReactionMap()
        {
            _reactionMap = new Dictionary<AiraBodyZone, ZoneReaction>();
            foreach (var r in _zoneReactions)
                if (!_reactionMap.ContainsKey(r.zone))
                    _reactionMap[r.zone] = r;
        }

        // Proses klik zona
        private void OnZoneClicked(AiraBodyZone zone)
        {
            if (_isOnCooldown) return;
            if (!_reactionMap.TryGetValue(zone, out ZoneReaction reaction)) return;
            StartCoroutine(PlayReaction(reaction));
        }

        // Mainkan reaksi Aira
        private IEnumerator PlayReaction(ZoneReaction reaction)
        {
            _isOnCooldown = true;

            string line = PickRandomLine(reaction.reactionLines);

            AiraController.Instance?.SetExpression(reaction.expression);
            GameManager.Instance?.ChangeState(GameManager.GameState.SPEAKING);
            TTSManager.Instance?.Speak(line, reaction.expression.Trim('[', ']'));

            yield return new WaitForSeconds(_reactionDuration);

            GameManager.Instance?.ChangeState(GameManager.GameState.IDLE);

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
