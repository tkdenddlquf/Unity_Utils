using UnityEngine;

public struct TouchInfo
{
    public int count;
    public Vector2 pos;

    public int fingerId;
    public TouchPhase phase;

    public GameObject gameObject;
}
