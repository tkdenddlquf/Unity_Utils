using System;
using UnityEngine;

[Serializable]
public class BindingValue<T>
{
    [SerializeField] private T value;

    public BindCallback callback;

    public T Data
    {
        get => value;
        set
        {
            if (callback == null) this.value = value;
            else callback(ref this.value, value);
        }
    }

    public void SetCallback(BindCallback callback)
    {
        this.callback = callback;
        this.callback(ref value, value);
    }

    public delegate void BindCallback(ref T currentValue, T newValue);
}
