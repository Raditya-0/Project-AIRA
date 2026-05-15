using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AIRA.MiniGames.SpaceShooter
{
    public class EnergyBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image       m_fillImage;
        [SerializeField] private CanvasGroup m_canvasGroup;

        [Header("World Tracking")]
        [SerializeField] private Camera    m_camera;
        [SerializeField] private Transform m_shipTransform;
        [SerializeField] private Vector3   m_offset = new Vector3(0f, -1.8f, 0f);

        [Header("Visibility")]
        [SerializeField] private float m_hideDelay    = 2f;
        [SerializeField] private float m_fadeDuration = 0.4f;

        [Header("Dependencies")]
        [SerializeField] private ShipBase m_ship;

        private RectTransform m_canvasRect;
        private RectTransform m_barRect;
        private float         m_hideTimer;
        private bool          m_visible;
        private Coroutine     m_fadeCoroutine;

        // Ambil referensi rect transform
        private void Awake()
        {
            m_canvasRect = GetComponent<RectTransform>();
            m_barRect    = m_canvasGroup != null ? m_canvasGroup.GetComponent<RectTransform>() : null;
        }

        // Sembunyikan bar di awal
        private void Start()
        {
            if (m_canvasGroup == null) return;
            m_canvasGroup.alpha          = 0f;
            m_canvasGroup.blocksRaycasts = false;
        }

        // Perbarui posisi, fill, dan visibilitas
        private void LateUpdate()
        {
            UpdateBarPosition();
            UpdateFillAmount();
            UpdateVisibility();
        }

        // Konversi posisi world ke layar
        private void UpdateBarPosition()
        {
            if (m_shipTransform == null || m_camera == null ||
                m_canvasRect == null  || m_barRect == null) return;

            Vector3 screenPos = m_camera.WorldToScreenPoint(m_shipTransform.position + m_offset);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_canvasRect, screenPos, m_camera, out Vector2 localPoint);
            m_barRect.anchoredPosition = localPoint;
        }

        // Perbarui nilai fill bar
        private void UpdateFillAmount()
        {
            if (m_fillImage == null || m_ship == null) return;
            float max = m_ship.GetMaxEnergy();
            m_fillImage.fillAmount = max > 0f ? m_ship.GetCurrentEnergy() / max : 0f;
        }

        // Tampil saat tidak penuh, hilang saat penuh
        private void UpdateVisibility()
        {
            if (m_ship == null) return;
            bool notFull = m_ship.GetCurrentEnergy() < m_ship.GetMaxEnergy();

            if (notFull)
            {
                m_hideTimer = m_hideDelay;
                SetVisible(true);
            }
            else
            {
                m_hideTimer -= Time.deltaTime;
                if (m_hideTimer <= 0f) SetVisible(false);
            }
        }

        // Toggle visibilitas dengan fade
        private void SetVisible(bool show)
        {
            if (m_visible == show) return;
            m_visible = show;
            if (m_fadeCoroutine != null) StopCoroutine(m_fadeCoroutine);
            m_fadeCoroutine = StartCoroutine(FadeCoroutine(show ? 1f : 0f));
        }

        // Animasi fade alpha canvasgroup
        private IEnumerator FadeCoroutine(float target)
        {
            float start   = m_canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < m_fadeDuration)
            {
                elapsed            += Time.deltaTime;
                m_canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / m_fadeDuration);
                yield return null;
            }
            m_canvasGroup.alpha          = target;
            m_canvasGroup.blocksRaycasts = target > 0f;
            m_fadeCoroutine              = null;
        }
    }
}
