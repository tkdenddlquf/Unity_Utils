using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : MonoBehaviour
{
    public System.Action<T> OnDequeue { private get; set; }
    public System.Action<T> OnEnqueue { private get; set; }

    private readonly T poolObject;

    private readonly Queue<T> poolObjects = new();

    public int Count => poolObjects.Count;

    public ObjectPool(T poolObject) => this.poolObject = poolObject;

    public void CreateDefault(Transform parent = null) => Enqueue(Object.Instantiate(poolObject, parent));

    public T Dequeue(Transform parent = null)
    {
        T @object = poolObjects.Count == 0 ? Object.Instantiate(poolObject, parent) : poolObjects.Dequeue();

        @object.transform.SetParent(parent);

        OnDequeue?.Invoke(@object);

        return @object;
    }

    public void Enqueue(T @object)
    {
        if (@object == null) return;

        OnEnqueue?.Invoke(@object);

        poolObjects.Enqueue(@object);
    }

    public void Clear()
    {
        while (Count != 0) Remove();
    }

    public void Remove()
    {
        Object.Destroy(poolObjects.Dequeue());
    }
}
