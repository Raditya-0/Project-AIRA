using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AIRA.MiniGames.SpaceShooter
{
    public class AiraHealthBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image       m_fillImage;
        [SerializeField] private CanvasGroup m_canvasGroup;

        [Header("World Tracking")]
        [SerializeField] private Camera    m_camera;
        [SerializeField] private Transform m_companionTransform;
        [SerializeField] private Vector3   m_offset = new Vector3(0f, -1.2f, 0f);

        [Header("Visibility")]
        [SerializeField] private float m_hideDelay    = 2f;
        [SerializeField] private float m_fadeDuration = 0.4f;

        [Header("Dependencies")]
        [SerializeField] private HealthManager m_healthManager;

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

        // Daftarkan event damage dan heal
        private void OnEnable()
        {
            GameEvents.Instance.onCompanionDamage += OnHealthChanged;
            GameEvents.Instance.onCompanionHeal   += OnHealthChanged;
            GameEvents.Instance.onRetry           += OnRetry;
        }

        // Lepas event saat nonaktif
        private void OnDisable()
        {
            GameEvents.Instance.onCompanionDamage -= OnHealthChanged;
            GameEvents.Instance.onCompanionHeal   -= OnHealthChanged;
            GameEvents.Instance.onRetry           -= OnRetry;
        }

        // Sembunyikan bar di awal
        private void Start()
        {
            if (m_canvasGroup == null) return;
            m_canvasGroup.alpha          = 0f;
            m_canvasGroup.blocksRaycasts = false;
        }

        // Perbarui posisi dan fill
        private void LateUpdate()
        {
            UpdateBarPosition();
            UpdateFillAmount();

            if (!m_visible) return;
            m_hideTimer -= Time.deltaTime;
            if (m_hideTimer <= 0f) SetVisible(false);
        }

        // Konversi posisi world ke layar
        private void UpdateBarPosition()
        {
            if (m_companionTransform == null || m_camera == null ||
                m_canvasRect == null         || m_barRect == null) return;

            Vector3 screenPos = m_camera.WorldToScreenPoint(m_companionTransform.position + m_offset);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_canvasRect, screenPos, m_camera, out Vector2 localPoint);
            m_barRect.anchoredPosition = localPoint;
        }

        // Perbarui nilai fill bar
        private void UpdateFillAmount()
        {
            if (m_fillImage == null || m_healthManager == null) return;
            float max = m_healthManager.GetAiraMaxHealth();
            m_fillImage.fillAmount = max > 0f ? m_healthManager.GetAiraCurrentHealth() / max : 0f;
        }

        // Tampilkan bar saat health berubah
        private void OnHealthChanged(float _)
        {
            m_hideTimer = m_hideDelay;
            SetVisible(true);
        }

        // Sembunyikan bar saat retry
        private void OnRetry()
        {
            SetVisible(false);
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
