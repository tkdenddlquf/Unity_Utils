using UnityEngine;

public class TouchInfo3D
{
    public int count;
    public int fingerId;
    public TouchPhase phase = TouchPhase.Ended;

    public int length = 0;
    public RaycastHit[] hits = new RaycastHit[5];

    public Vector3 origin;
    public Vector3 direction;

    public Vector3 GetPos(int index)
    {
        if (length <= index) return Vector3.zero;

        return hits[index].point;
    }

    public Vector3 GetSpecificYPos(float yPos)
    {
        if (direction.y == 0) return origin;

        float t = (yPos - origin.y) / direction.y;

        return origin + direction * t;
    }

    public GameObject this[int index]
    {
        get
        {
            if (length <= index) return null;

            if (hits[index].collider == null) return null;
            else return hits[index].collider.gameObject;
        }
    }
}
