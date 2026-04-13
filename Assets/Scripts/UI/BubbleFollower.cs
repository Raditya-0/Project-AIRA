using UnityEngine;

public class BubbleFollower : MonoBehaviour
{
    // Inspector
    [Header("Target")]
    [SerializeField] private Transform _characterRoot;
    [SerializeField] private Transform _headBone;

    [Header("Offset from anchor")]
    [SerializeField] private Vector3 _worldOffset = new Vector3(0.5f, 2.5f, 0f);

    [Header("Smoothing")]
    [SerializeField] private float _followSpeed = 8f;

    [Header("Screen Space Clamp")]
    [SerializeField] private float _padding = 20f;

    // Private
    private RectTransform _rectTransform;
    private Canvas        _canvas;
    private Camera        _mainCamera;

    // Lifecycle
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas        = GetComponentInParent<Canvas>();
        _mainCamera    = Camera.main;
    }

    private void LateUpdate()
    {
        if (_canvas == null || _mainCamera == null) return;

        Transform anchor = (_headBone != null) ? _headBone : _characterRoot;
        if (anchor == null) return;

        Vector3 worldPos = anchor.position
            + anchor.TransformDirection(_worldOffset);

        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector2 canvasPos;
        RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _mainCamera,
            out canvasPos
        );

        Rect rect = canvasRect.rect;
        canvasPos.x = Mathf.Clamp(canvasPos.x, rect.xMin + _padding, rect.xMax - _padding);
        canvasPos.y = Mathf.Clamp(canvasPos.y, rect.yMin + _padding, rect.yMax - _padding);

        _rectTransform.localPosition = Vector3.Lerp(
            _rectTransform.localPosition,
            new Vector3(canvasPos.x, canvasPos.y, _rectTransform.localPosition.z),
            Time.deltaTime * _followSpeed
        );
    }
}
