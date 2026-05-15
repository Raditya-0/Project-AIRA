using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AIRA.UI;
using AIRA.Voice;

public class MicToggleButton : CustomToggle, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Sprites")]
    [SerializeField] private Sprite _spriteOnHover;
    [SerializeField] private Sprite _spriteOffHover;

    private bool _isHovering;
    private bool _currentState;
    private Image _image;

    // Daftar semua event listener
    private void OnEnable()
    {
        if (_image == null) _image = GetComponent<Image>();
        STTManager.OnListeningStateChanged += OnSTTListeningStateChanged;
        OnValueChanged += OnToggleValueChanged;
    }

    // Hapus semua event listener
    private void OnDisable()
    {
        STTManager.OnListeningStateChanged -= OnSTTListeningStateChanged;
        OnValueChanged -= OnToggleValueChanged;
    }

    // Sync state dari STTManager
    private void OnSTTListeningStateChanged(bool isOn)
    {
        _currentState = isOn;
        SetState(isOn);
    }

    // Trigger STTManager saat toggle berubah
    private void OnToggleValueChanged(bool isOn)
    {
        _currentState = isOn;
        if (isOn) STTManager.Instance?.StartListening();
        else STTManager.Instance?.StopListening();
    }

    // Aktifkan hover state
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        UpdateVisuals();
    }

    // Nonaktifkan hover state
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        UpdateVisuals();
    }

    // Update sprite saat hover
    private void UpdateVisuals()
    {
        if (_image == null || !_isHovering) return;
        _image.sprite = _currentState ? _spriteOnHover : _spriteOffHover;
    }
}
