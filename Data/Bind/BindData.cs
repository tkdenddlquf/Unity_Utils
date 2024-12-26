using System;
using UnityEngine;

[Serializable]
public class BindData<T>
{
    [SerializeField] private T value;

    private BindCallback callback;

    public T Value
    {
        get => value;
        set
        {
            if (callback == null) this.value = value;
            else callback(ref this.value, value);
        }
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
        }

        this.callback(ref value, value);
    }

    public delegate void BindCallback(ref T currentValue, T newValue);
}
