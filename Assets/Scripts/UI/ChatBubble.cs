using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ChatBubble : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _label;

    public void SetText(string text)
    {
        if (_label != null) _label.text = text;
    }

    public string GetText() => _label != null ? _label.text : string.Empty;
}
