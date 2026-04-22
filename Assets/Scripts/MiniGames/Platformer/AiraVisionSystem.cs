using System.Collections.Generic;
using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    [System.Serializable]
    public class VisionTarget
    {
        public string    label;
        public Transform target;
        public bool      isObstacle;
    }

    public class AiraVisionSystem : MonoBehaviour
    {
        [Header("Vision Settings")]
        [SerializeField] private float     _visionRadius   = 15f;
        [SerializeField] private LayerMask _detectLayer;

        [Header("References")]
        [SerializeField] private Transform _keyTransform;
        [SerializeField] private Transform _endPointTransform;

        [Header("Vision Targets")]
        [SerializeField] private List<VisionTarget> _visionTargets = new();

        [Header("Update Interval")]
        [SerializeField] private float _updateInterval = 0.1f;

        // Hasil deteksi publik
        public bool   CanSeeKey          { get; private set; }
        public bool   CanSeeEnd          { get; private set; }
        public float  KeyDistance        { get; private set; }
        public float  EndDistance        { get; private set; }
        public string KeyDirection       { get; private set; }
        public string EndDirection       { get; private set; }
        public bool   HasObstacleAhead          { get; private set; }
        public float  NearestObstacleDirection  { get; private set; }

        private float _timer;

        // Update deteksi per interval
        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < _updateInterval) return;
            _timer = 0f;
            UpdateVision();
        }

        // Deteksi posisi key dan endpoint
        private void UpdateVision()
        {
            CanSeeKey               = false;
            CanSeeEnd               = false;
            HasObstacleAhead        = false;
            NearestObstacleDirection = 0f;

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

            // Deteksi semua vision target generic
            foreach (var vt in _visionTargets)
            {
                if (vt.target == null) continue;
                float dist = Vector2.Distance(transform.position, vt.target.position);
                if (dist > _visionRadius) continue;

                if (vt.isObstacle)
                {
                    float dx = vt.target.position.x - transform.position.x;
                    float dy = vt.target.position.y - transform.position.y;
                    bool isAhead = Mathf.Abs(dx) < 3f && dy > -1f && dy < 2f;
                    if (isAhead)
                    {
                        HasObstacleAhead         = true;
                        NearestObstacleDirection = dx;
                    }
                }
            }
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
            var parts = new System.Text.StringBuilder();

            if (CanSeeKey)
                parts.Append($"I can see the key {KeyDirection}, {DescribeDistance(KeyDistance)}. ");

            if (CanSeeEnd)
                parts.Append($"I can see the goal {EndDirection}, {DescribeDistance(EndDistance)}. ");

            // Tambah semua target terdeteksi
            foreach (var vt in _visionTargets)
            {
                if (vt.target == null) continue;
                float dist = Vector2.Distance(transform.position, vt.target.position);
                if (dist > _visionRadius) continue;

                string dir = GetRelativeDirection(vt.target.position);
                parts.Append($"I can see a {vt.label} {dir}, {DescribeDistance(dist)}. ");
            }

            string result = parts.ToString().Trim();
            return result.Length > 0 ? result : "I can't see anything important from here.";
        }
    }
}
