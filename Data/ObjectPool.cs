using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : Object
{
    public System.Action<T> DeAction { private get; set; }
    public System.Action<T> EnAction { private get; set; }

    private readonly Transform parent;
    private readonly T poolObject;

    private readonly Queue<T> poolObjects = new();

    public ObjectPool(T _poolObject, Transform _parent)
    {
        poolObject = _poolObject;
        parent = _parent;
    }

    public T Dequeue()
    {
        if (poolObjects.Count == 0) poolObjects.Enqueue(Object.Instantiate(poolObject, parent));

        DeAction?.Invoke(poolObjects.Peek());

        return poolObjects.Dequeue();
    }

    public void Enqueue(T _object)
    {
        if (_object == null) return;

        EnAction?.Invoke(_object);

        poolObjects.Enqueue(_object);
    }
}
