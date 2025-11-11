using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Mobile joystick that safely finds or creates a handle Image child.
/// Attach to JoystickBG (Image). Child JoystickHandle is recommended but optional.
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
        // try find child named JoystickHandle first
        Transform t = transform.Find("JoystickHandle");
        if (t != null) handleImage = t.GetComponent<Image>();

        // otherwise find any Image child
        if (handleImage == null)
        {
            foreach (Transform child in transform)
            {
                var img = child.GetComponent<Image>();
                if (img != null) { handleImage = img; break; }
            }
        }

        // If none found, create a default handle GameObject
        if (handleImage == null)
        {
            GameObject go = new GameObject("JoystickHandle", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            handleImage = go.GetComponent<Image>();

            // default visuals (transparent white) — you should replace sprite in inspector
            handleImage.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);

            // configure RectTransform defaults
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(150, 150);
            rt.anchoredPosition = Vector2.zero;
        }
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

    public float Horizontal() => inputVector.x;
    public float Vertical() => inputVector.y;
    public Vector2 Direction() => inputVector;
    public float Magnitude() => inputVector.magnitude;
}
