using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public class AiraVisionSystem : MonoBehaviour
    {
        [Header("Vision Settings")]
        [SerializeField] private float     _visionRadius  = 15f;

        [Header("References")]
        [SerializeField] private Transform _keyTransform;
        [SerializeField] private Transform _endPointTransform;

        [Header("Update Interval")]
        [SerializeField] private float _updateInterval = 0.1f;

        // Akses endpoint untuk planner
        public Transform EndPointTransform => _endPointTransform;

        // Hasil deteksi publik
        public bool   CanSeeKey    { get; private set; }
        public bool   CanSeeEnd    { get; private set; }
        public float  KeyDistance  { get; private set; }
        public float  EndDistance  { get; private set; }
        public string KeyDirection { get; private set; }
        public string EndDirection { get; private set; }

        // Semua plate dalam radius vision
        public List<PressurePlate> VisibleButtons { get; private set; } = new();

        // Reactive terdekat yang blocking path Aira ke endpoint
        public PlateReactive NearestBlockingWall { get; private set; }

        private float _timer;

        // Update deteksi per interval
        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _updateInterval) return;
            _timer = 0f;
            UpdateVision();
        }

        // Deteksi posisi key, endpoint, dan interactable
        private void UpdateVision()
        {
            CanSeeKey = false;
            CanSeeEnd = false;
            NearestBlockingWall = null;

            if (_keyTransform != null && _keyTransform.gameObject.activeSelf)
            {
                float dist = Vector2.Distance(transform.position, _keyTransform.position);
                if (dist <= _visionRadius)
                {
                    CanSeeKey    = true;
                    KeyDistance  = dist;
                    KeyDirection = GetRelativeDirection(_keyTransform.position);
                }
            }

            if (_endPointTransform != null)
            {
                float dist = Vector2.Distance(transform.position, _endPointTransform.position);
                if (dist <= _visionRadius)
                {
                    CanSeeEnd    = true;
                    EndDistance  = dist;
                    EndDirection = GetRelativeDirection(_endPointTransform.position);
                }
            }

            UpdateInteractables();
        }

        // Perbarui daftar interactable terlihat
        private void UpdateInteractables()
        {
            VisibleButtons = InteractableRegistry
                .GetPlatesInRadius(transform.position, _visionRadius);

            var reactives = InteractableRegistry
                .GetReactivesInRadius(transform.position, _visionRadius);

            NearestBlockingWall = reactives
                .Where(r => r.IsBlocking)
                .Where(r => IsBlockingPath(r.transform.position))
                .OrderBy(r => Vector2.Distance(transform.position, r.transform.position))
                .FirstOrDefault();
        }

        // Cek apakah reactive ada di antara Aira dan endpoint
        private bool IsBlockingPath(Vector2 wallPos)
        {
            if (_endPointTransform == null) return false;
            float airaX = transform.position.x;
            float endX  = _endPointTransform.position.x;
            float wallX = wallPos.x;
            float minX  = Mathf.Min(airaX, endX);
            float maxX  = Mathf.Max(airaX, endX);
            return wallX > minX && wallX < maxX;
        }

        // Terjemahkan posisi ke arah relatif
        private string GetRelativeDirection(Vector2 targetPos)
        {
            float dx = targetPos.x - transform.position.x;
            if (Mathf.Abs(dx) < 1f) return "ahead";
            return dx > 0f ? "to the right" : "to the left";
        }

        // Terjemahkan jarak ke deskripsi
        private string DescribeDistance(float dist)
        {
            if (dist < 3f)  return "very close";
            if (dist < 7f)  return "nearby";
            if (dist < 12f) return "a bit far";
            return "very far";
        }

        // Build konteks vision untuk LLM
        public string BuildVisionContext()
        {
            var sb = new System.Text.StringBuilder();

            if (CanSeeKey)
                sb.Append($"I can see the key {KeyDirection}, {DescribeDistance(KeyDistance)}. ");

            if (CanSeeEnd)
                sb.Append($"I can see the goal {EndDirection}, {DescribeDistance(EndDistance)}. ");

            foreach (var plate in VisibleButtons)
            {
                string dir    = GetRelativeDirection(plate.transform.position);
                string dist   = DescribeDistance(
                    Vector2.Distance(transform.position, plate.transform.position));
                string effect = plate.affects != null
                    ? $" that {plate.effectDesc}"
                    : "";
                sb.Append($"I can see a button{effect} {dir}, {dist}. ");
            }

            if (NearestBlockingWall != null)
            {
                string dir = GetRelativeDirection(NearestBlockingWall.transform.position);
                sb.Append($"There is a {NearestBlockingWall.blockDesc} {dir}. ");
            }

            string result = sb.ToString().Trim();
            return result.Length > 0 ? result : "I can't see anything important from here.";
        }
    }
}
