using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    public class PlateReactive : MonoBehaviour
    {
        [SerializeField] private Vector2 _offsetOff = Vector2.zero;
        [SerializeField] private Vector2 _offsetOn;
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private PressurePlate _linkedPlate;

        private Vector2 _startPosition;
        private Vector2 _currentOffset;

        // simpan posisi awal objek
        private void Awake()
        {
            _startPosition = transform.position;
            _currentOffset = _offsetOff;
        }

        // subscribe event plate
        private void OnEnable()
        {
            if (_linkedPlate == null) return;
            _linkedPlate.OnPressed.AddListener(MoveToOn);
            _linkedPlate.OnReleased.AddListener(MoveToOff);
        }

        // unsubscribe event plate
        private void OnDisable()
        {
            if (_linkedPlate == null) return;
            _linkedPlate.OnPressed.RemoveListener(MoveToOn);
            _linkedPlate.OnReleased.RemoveListener(MoveToOff);
        }

        // lerp posisi tiap frame
        private void Update()
        {
            transform.position = Vector2.Lerp(transform.position, _startPosition + _currentOffset, _moveSpeed * Time.deltaTime);
        }

        // set offset posisi aktif
        private void MoveToOn()
        {
            _currentOffset = _offsetOn;
        }

        // set offset posisi nonaktif
        private void MoveToOff()
        {
            _currentOffset = _offsetOff;
        }
    }
}
