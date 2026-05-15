using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ButtonSpriteController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Image _targetImage;

    public Sprite defaultSprite;
    public Sprite hoverSprite;
    public Sprite clickSprite;

    void Awake()
    {
        _targetImage = GetComponent<Image>();
    }

    void Start()
    {
        if (_targetImage != null && defaultSprite != null)
            _targetImage.sprite = defaultSprite;
    }

    void OnEnable()
    {
        if (_targetImage != null && defaultSprite != null)
            _targetImage.sprite = defaultSprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_targetImage != null && hoverSprite != null)
            _targetImage.sprite = hoverSprite;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_targetImage != null && defaultSprite != null)
            _targetImage.sprite = defaultSprite;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_targetImage != null && clickSprite != null)
            _targetImage.sprite = clickSprite;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_targetImage != null && defaultSprite != null)
            _targetImage.sprite = defaultSprite;
    }
}