using UnityEngine;

namespace AIRA.Utils
{
    public class ScrollingTilemap : MonoBehaviour
    {
        [SerializeField] private float _scrollSpeed   = 2f;
        [SerializeField] private float _tilemapHeight;

        private Vector3 _startPosition;

        // Simpan posisi awal tilemap
        private void Start()
        {
            _startPosition = transform.position;
        }

        // Geser dan loop tilemap
        private void Update()
        {
            transform.position += Vector3.down * _scrollSpeed * Time.deltaTime;

            if (transform.position.y <= _startPosition.y - _tilemapHeight)
                transform.position = _startPosition;
        }
    }
}
