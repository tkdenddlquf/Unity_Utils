using UnityEngine;

public static class TouchSystem
{
    public static Vector2 TouchBeganPosition { get; private set; }

    private static Camera mainCamera;

#if UNITY_EDITOR
    public static TouchInfo GetTouch(int index, int layerMask = -1)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        TouchInfo info = new();
        Vector2 pos = Input.mousePosition;

        if (pos.x > mainCamera.scaledPixelWidth || pos.y > mainCamera.scaledPixelHeight) return info;

        info.pos = mainCamera.ScreenToWorldPoint(pos);

        if (Input.GetMouseButton(index))
        {
            if (Input.GetMouseButtonDown(index))
            {
                info.phase = TouchPhase.Began;
                TouchBeganPosition = info.pos;
            }
            else info.phase = TouchBeganPosition == info.pos ? TouchPhase.Stationary : TouchPhase.Moved;

            info.count = index + 1;
            info.fingerId = -1;
        }

        if (info.count > index)
        {
            RaycastHit2D hit = Physics2D.Raycast(info.pos, Vector2.zero, 1f, layerMask);

            if (hit.collider != null) info.gameObject = hit.collider.gameObject;
        }
        else info.phase = TouchBeganPosition == info.pos ? TouchPhase.Canceled : TouchPhase.Ended;

        return info;
    }
#else
    public static TouchInfo GetTouch(int index, int layerMask = -1)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        TouchInfo info = new() { count = Input.touchCount };

        if (info.count > index)
        {
            Touch touch = Input.GetTouch(index);
            
            info.fingerId = touch.fingerId;
            info.phase = touch.phase;
            info.pos = mainCamera.ScreenToWorldPoint(touch.position);

            if (info.phase == TouchPhase.Began) TouchBeganPosition = info.pos;

            RaycastHit2D hit = Physics2D.Raycast(info.pos, Vector2.zero, 1f, layerMask);

            if (hit.collider != null) info.gameObject = hit.collider.gameObject;
        }

        return info;
    }
#endif
}
