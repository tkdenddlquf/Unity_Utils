using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RotateImage : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] private Image image;
    [SerializeField] private GameObject ratateObject;

    [SerializeField, Range(0, 1)] private float value = 1;

    [Header("Click Setting")]
    [SerializeField] private bool clickable;
    [SerializeField] private float offsetAngle;

    [Header("Hover Setting")]
    [SerializeField] private bool hoverEvent;
    [SerializeField, Space(5)] private UnityEvent<float, Vector2> enterEvent;
    [SerializeField, Space(5)] private UnityEvent exitEvent;

    private void OnValidate()
    {
        if (image == null || ratateObject == null) return;

        SetFilled(value);
    }

    public void SetFilled(float value)
    {
        value = Mathf.Clamp01(value);

        image.fillAmount = value;

        if (image.fillClockwise) ratateObject.transform.localEulerAngles = (1 - value) * 360 * Vector3.forward;
        else ratateObject.transform.localEulerAngles = (value - 1) * 360 * Vector3.forward;
    }

    public void OnPointerDown(PointerEventData eventData) => SetFilled(eventData);

    public void OnDrag(PointerEventData eventData) => SetFilled(eventData);

    private void SetFilled(PointerEventData eventData)
    {
        if (!clickable) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
        {
            float angle = Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;

            angle -= 90 * (image.fillOrigin + 2);
            angle += offsetAngle;

            angle = Mathf.Repeat(angle, 360f);

            if (image.fillClockwise) angle = 360 - angle;

            SetFilled(angle / 360);
        }

        if (hoverEvent) enterEvent?.Invoke(image.fillAmount, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (hoverEvent) enterEvent?.Invoke(image.fillAmount, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverEvent) exitEvent?.Invoke();
    }
}
