using UnityEngine;

namespace AIRA.MiniGames.Platformer
{
    [RequireComponent(typeof(Collider2D))]
    public class StackingSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AiraAIController _airaAI;

        private Rigidbody2D _airaRb;
        private int         _playerContactCount;

        // Ambil komponen Rigidbody2D
        private void Awake()
        {
            _airaRb = GetComponent<Rigidbody2D>();

            if (_airaAI == null)
                _airaAI = GetComponent<AiraAIController>();
        }

        // Deteksi player di atas
        private void OnCollisionEnter2D(Collision2D col)
        {
            if (!col.gameObject.CompareTag("Player")) return;

            // Cek player datang dari atas
            if (!IsContactFromAbove(col)) return;

            _playerContactCount++;
            if (_playerContactCount == 1)
                FreezeAira();
        }

        // Deteksi player pergi dari atas
        private void OnCollisionExit2D(Collision2D col)
        {
            if (!col.gameObject.CompareTag("Player")) return;

            _playerContactCount = Mathf.Max(0, _playerContactCount - 1);
            if (_playerContactCount == 0)
                UnfreezeAira();
        }

        // Cek kontak dari arah atas
        private bool IsContactFromAbove(Collision2D col)
        {
            foreach (ContactPoint2D contact in col.contacts)
            {
                if (contact.normal.y < -0.5f) return true;
            }
            return false;
        }

        // Freeze Aira jadi base
        private void FreezeAira()
        {
            if (_airaRb != null)
                _airaRb.constraints = RigidbodyConstraints2D.FreezeAll;

            _airaAI?.OnPlayerStacking();
            Debug.Log("[StackingSystem] Aira difreeze jadi base.");
        }

        // Unfreeze setelah player pergi
        private void UnfreezeAira()
        {
            if (_airaRb != null)
                _airaRb.constraints = RigidbodyConstraints2D.FreezeRotation;

            _airaAI?.OnPlayerLeft();
            Debug.Log("[StackingSystem] Aira diunfreeze.");
        }
    }
}
