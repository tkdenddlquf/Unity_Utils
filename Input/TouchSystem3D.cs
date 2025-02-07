using System.Collections.Generic;
using UnityEngine;

public static class TouchSystem3D
{
    public readonly static Dictionary<int, TouchInfo3D> infos = new();

    private static Vector2 beganPosition;
    private static Camera mainCamera;

#if UNITY_EDITOR
    public static TouchInfo3D GetTouch(int index, int layerMask = -1)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        Vector2 pos = Input.mousePosition;
        Vector2 screenScale = new(mainCamera.scaledPixelWidth, mainCamera.scaledPixelHeight);

        if (!infos.ContainsKey(layerMask)) infos.Add(layerMask, new());

        if (pos.x > screenScale.x || pos.y > screenScale.y) return infos[layerMask];

        Ray ray = mainCamera.ScreenPointToRay(pos);

        infos[layerMask].origin = ray.origin;
        infos[layerMask].direction = ray.direction;

        if (Input.GetMouseButton(index))
        {
            infos[layerMask].length = Physics.RaycastNonAlloc(ray, infos[layerMask].hits, Mathf.Infinity, layerMask);

            infos[layerMask].phase = (infos[layerMask].phase == TouchPhase.Began || infos[layerMask].phase == TouchPhase.Stationary) && beganPosition == pos ? TouchPhase.Stationary : TouchPhase.Moved;

            if (Input.GetMouseButtonDown(index))
            {
                infos[layerMask].phase = TouchPhase.Began;
                beganPosition = pos;
            }

            infos[layerMask].count = index + 1;
            infos[layerMask].fingerId = -1;
        }
        else
        {
            infos[layerMask].count = 0;
            infos[layerMask].phase = infos[layerMask].phase == TouchPhase.Stationary && beganPosition == pos ? TouchPhase.Canceled : TouchPhase.Ended;
        }

        return infos[layerMask];
    }
#else
    public static TouchInfo3D GetTouch(int index, int layerMask = -1)
    {
        if (mainCamera == null) mainCamera = Camera.main;

        if (!infos.ContainsKey(layerMask)) infos.Add(layerMask, new());

        infos[layerMask].count = Input.touchCount;

        if (Input.touchCount > index)
        {
            Touch touch = Input.GetTouch(index);
            
            infos[layerMask].fingerId = touch.fingerId;
            infos[layerMask].phase = touch.phase;

            Ray ray = mainCamera.ScreenPointToRay(touch.position);

            infos[layerMask].origin = ray.origin;
            infos[layerMask].direction = ray.direction;
            
            infos[layerMask].length = Physics.RaycastNonAlloc(ray, infos[layerMask].hits, Mathf.Infinity, layerMask);
        }
        else infos[layerMask].phase = infos[layerMask].phase == TouchPhase.Stationary && beganPosition == pos ? TouchPhase.Canceled : TouchPhase.Ended;

        return infos[layerMask];
    }
#endif
}
