using UnityEngine;

public struct TouchInfo
{
    public int count;
    public Vector2 pos;

    public int fingerId;
    public TouchPhase phase;

    public RaycastHit2D[] hits;

    public readonly GameObject this[int index]
    {
        get
        {
            if (hits == null || hits.Length <= index) return null;

            if (hits[index].collider == null) return null;
            else return hits[index].collider.gameObject;
        }
    }
}
