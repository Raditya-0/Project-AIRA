using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AIRA.MiniGames.Platformer
{
    public class PlateReactive : MonoBehaviour
    {
        [SerializeField] private Vector2 _offsetOff = Vector2.zero;
        [SerializeField] private Vector2 _offsetOn;
        [SerializeField] private float _moveSpeed = 5f;

        [SerializeField] public string blockDesc = "blocks path";

        // Daftar plate yang dipantau
        [SerializeField] private List<PressurePlate> _linkedPlates = new();

        private Vector2 _startPosition;
        private Vector2 _currentOffset;

        // Hitung jumlah plate aktif
        private int _activePlateCount;

        // Cek apakah object sedang blocking
        public bool IsBlocking =>
            Vector2.Distance(transform.position, _startPosition + _offsetOff) < 0.1f;

        // Simpan posisi awal objek
        private void Awake()
        {
            _startPosition  = transform.position;
            _currentOffset  = _offsetOff;
        }

        // Subscribe plate dan daftarkan ke registry
        private void OnEnable()
        {
            InteractableRegistry.RegisterReactive(this);
            foreach (var plate in _linkedPlates)
            {
                if (plate == null) continue;
                plate.OnPressed.AddListener(OnAnyPlatePressed);
                plate.OnReleased.AddListener(OnAnyPlateReleased);
            }
        }

        // Unsubscribe plate dan hapus dari registry
        private void OnDisable()
        {
            InteractableRegistry.UnregisterReactive(this);
            foreach (var plate in _linkedPlates)
            {
                if (plate == null) continue;
                plate.OnPressed.RemoveListener(OnAnyPlatePressed);
                plate.OnReleased.RemoveListener(OnAnyPlateReleased);
            }
        }

        // Lerp posisi tiap frame
        private void Update()
        {
            transform.position = Vector2.Lerp(
                transform.position,
                _startPosition + _currentOffset,
                _moveSpeed * Time.deltaTime
            );
        }

        // Tambah hitungan plate aktif
        private void OnAnyPlatePressed()
        {
            _activePlateCount++;
            if (_activePlateCount == 1)
                _currentOffset = _offsetOn;
        }

        // Kurangi hitungan plate aktif
        private void OnAnyPlateReleased()
        {
            _activePlateCount = Mathf.Max(0, _activePlateCount - 1);
            if (_activePlateCount == 0)
                _currentOffset = _offsetOff;
        }
    }
}