using UnityEngine;
using UnityEngine.UI;
using AIRA.Voice;

public class MicToggleButton : MonoBehaviour
{
    [SerializeField] private Image _buttonImage;

    [Header("Warna Tombol")]
    [SerializeField] private Color _colorOn  = new Color(0.2f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color _colorOff = new Color(0.8f, 0.2f, 0.2f, 1f);

    private bool _isActive = false;

    // Subscribe event STTManager
    private void OnEnable()
    {
        STTManager.OnListeningStateChanged += SetActive;
    }

    // Lepas event STTManager
    private void OnDisable()
    {
        STTManager.OnListeningStateChanged -= SetActive;
    }

    // Tombol ditekan user
    public void OnClick()
    {
        _isActive = !_isActive;
        UpdateColor();

        if (_isActive)
            STTManager.Instance?.StartListening();
        else
            STTManager.Instance?.StopListening();
    }

    // Sync warna dari luar
    public void SetActive(bool active)
    {
        _isActive = active;
        UpdateColor();
    }

    // Terapkan warna ke image
    private void UpdateColor()
    {
        if (_buttonImage != null)
            _buttonImage.color = _isActive ? _colorOn : _colorOff;
    }
}
