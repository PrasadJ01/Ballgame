using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Mobile joystick (base + handle). Attach to the JoystickBG Image object.
/// Must be childed: JoystickBG (this) -> JoystickHandle (Image child).
/// </summary>
public class MobileJoystick : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    private Image bgImage;
    private Image handleImage;
    private Vector2 inputVector = Vector2.zero;

    [Tooltip("How far the handle can move inside the background (as fraction of bg size).")]
    public float handleMoveRange = 0.33f;

    void Awake()
    {
        bgImage = GetComponent<Image>();
        if (transform.childCount > 0)
            handleImage = transform.GetChild(0).GetComponent<Image>();
        else
            Debug.LogError("MobileJoystick: JoystickHandle child missing.");
    }

    public void OnDrag(PointerEventData ped)
    {
        if (bgImage == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bgImage.rectTransform, ped.position, ped.pressEventCamera, out localPoint);

        // Normalize local point to -1..1 based on bg size
        float px = localPoint.x / bgImage.rectTransform.sizeDelta.x;
        float py = localPoint.y / bgImage.rectTransform.sizeDelta.y;

        // Convert to -1..1
        Vector2 normalized = new Vector2(px * 2f, py * 2f);
        inputVector = (normalized.magnitude > 1f) ? normalized.normalized : normalized;

        // Set handle anchored position limited by handleMoveRange
        if (handleImage != null)
        {
            float maxX = bgImage.rectTransform.sizeDelta.x * handleMoveRange;
            float maxY = bgImage.rectTransform.sizeDelta.y * handleMoveRange;
            handleImage.rectTransform.anchoredPosition = new Vector2(inputVector.x * maxX, inputVector.y * maxY);
        }
    }

    public void OnPointerDown(PointerEventData ped) => OnDrag(ped);

    public void OnPointerUp(PointerEventData ped)
    {
        inputVector = Vector2.zero;
        if (handleImage != null)
            handleImage.rectTransform.anchoredPosition = Vector2.zero;
    }

    /// <summary>Horizontal input -1..1</summary>
    public float Horizontal() => inputVector.x;
    /// <summary>Vertical input -1..1</summary>
    public float Vertical() => inputVector.y;
    /// <summary>2D Direction vector</summary>
    public Vector2 Direction() => inputVector;
    /// <summary>Magnitude 0..1</summary>
    public float Magnitude() => inputVector.magnitude;
}
