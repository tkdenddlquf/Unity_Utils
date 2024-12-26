using System;
using UnityEngine;

[Serializable]
public class BindData<T>
{
    [SerializeField] private T value;

    private BindCallback callback = (ref T currentValue, T newValue) => currentValue = newValue;

    public T Value
    {
        get => value;
        set => callback(ref this.value, value);
    }

    public void SetCallback(BindCallback callback, SetCallbackType type = SetCallbackType.Set)
    {
        switch (type)
        {
            case SetCallbackType.Set:
                this.callback = callback;
                break;

            case SetCallbackType.Add:
                this.callback += callback;
                break;

            case SetCallbackType.Remove:
                this.callback -= callback;
                break;

            default:
                return;
        }

        this.callback(ref value, value);
    }

    public delegate void BindCallback(ref T currentValue, T newValue);
}
