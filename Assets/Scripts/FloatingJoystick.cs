using UnityEngine;
using UnityEngine.EventSystems;

public class FloatingJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("UI References")]
    public RectTransform background;   // assign the big ring UI RectTransform
    public RectTransform handle;       // assign the small knob UI RectTransform

    [Header("Settings")]
    public float handleRange = 60f;    // px - how far handle can move from center
    public bool isFloating = true;     // if true, joystick appears where you touch
    public Vector2 fixedAnchoredPosition = new Vector2(-150, 120); // anchored pos when fixed (bottom-right)

    Canvas canvas;
    Vector2 input = Vector2.zero;
    int pointerId = -1;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) Debug.LogError("FloatingJoystick must be a child of a Canvas.");
        if (!background || !handle) Debug.LogError("Assign background and handle RectTransforms in inspector.");

        if (!isFloating)
        {
            background.gameObject.SetActive(true);
            background.anchoredPosition = fixedAnchoredPosition;
            handle.anchoredPosition = Vector2.zero;
        }
        else
        {
            background.gameObject.SetActive(false);
        }
    }

    public Vector2 Direction => input;
    public float Magnitude => input.magnitude;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerId != -1) return;
        pointerId = eventData.pointerId;

        Vector2 localPoint;
        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, canvas.worldCamera, out localPoint);

        if (isFloating)
        {
            background.anchoredPosition = localPoint;
            background.gameObject.SetActive(true);
            handle.anchoredPosition = Vector2.zero;
        }
        UpdateHandle(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        UpdateHandle(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        pointerId = -1;
        input = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
        if (isFloating) background.gameObject.SetActive(false);
    }

    void UpdateHandle(PointerEventData eventData)
    {
        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, canvas.worldCamera, out local);

        Vector2 delta = local - background.anchoredPosition;
        Vector2 clamped = Vector2.ClampMagnitude(delta, handleRange);
        handle.anchoredPosition = clamped;
        input = clamped / handleRange;
    }
}
