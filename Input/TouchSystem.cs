using UnityEngine;

public static class TouchSystem
{
    private static Camera mainCamera;

#if UNITY_EDITOR
    private Vector3 clickPos;

    public static TouchInfo GetTouch(int index)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        TouchInfo info = new();

        if (Input.GetMouseButton(index))
        {
            if (Input.GetMouseButtonDown(index))
            {
                info.phase = TouchPhase.Began;
                clickPos = Input.mousePosition;
            }
            else info.phase = clickPos == Input.mousePosition ? TouchPhase.Stationary : TouchPhase.Moved;

            info.count = index + 1;
        }

        if (info.count > index)
        {
            info.pos = mainCamera.ScreenToWorldPoint(Input.mousePosition);

            RaycastHit2D hit = Physics2D.Raycast(info.pos, Vector2.zero);

            if (hit.collider is not null) info.gameObject = hit.collider.gameObject;
        }
        else info.phase = clickPos == Input.mousePosition ? TouchPhase.Canceled : TouchPhase.Ended;

        return info;
    }
#else
    public static TouchInfo GetTouch(int index)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        TouchInfo info = new() { count = Input.touchCount };

        if (info.count > index)
        {
            Touch touch = Input.GetTouch(index);

            info.phase = touch.phase;
            info.pos = mainCamera.ScreenToWorldPoint(touch.position);

            RaycastHit2D hit = Physics2D.Raycast(info.pos, Vector2.zero);

            if (hit.collider != null) info.gameObject = hit.collider.gameObject;
        }

        return info;
    }
#endif
}
