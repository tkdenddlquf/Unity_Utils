using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : Object
{
    private readonly T poolObject;
    private readonly Transform parent;

    private readonly Queue<T> poolObjects = new();

    public ObjectPool(T _poolObject, Transform _parent)
    {
        poolObject = _poolObject;
        parent = _parent;
    }

    public T Dequeue()
    {
        if (poolObjects.Count == 0) poolObjects.Enqueue(Object.Instantiate(poolObject, parent));

        return poolObjects.Dequeue();
    }

    public void Enqueue(T _object)
    {
        poolObjects.Enqueue(_object);
    }
}
