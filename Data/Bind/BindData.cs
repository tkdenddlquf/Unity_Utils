using System;
using UnityEngine;

[Serializable]
public class BindData<T>
{
    [SerializeField] private T value;

    private PrevCallback prevCallback = (ref T currentValue, T newValue) => currentValue = newValue;
    private SubCallback subCallback;

    public T Value
    {
        get => value;
        set
        {
            prevCallback?.Invoke(ref this.value, value);
            subCallback?.Invoke(this.value);
        }
    }

    public BindData() { }

    public BindData(T value) { this.value = value; }

    public void SetCallback(PrevCallback callback, SetCallbackType type = SetCallbackType.Set)
    {
        switch (type)
        {
            case SetCallbackType.Set:
                prevCallback = callback;
                break;

            case SetCallbackType.Add:
                prevCallback += callback;
                break;

            case SetCallbackType.Remove:
                prevCallback -= callback;
                break;

            default:
                return;
        }

        callback?.Invoke(ref value, value);
    }

    public void SetCallback(SubCallback callback, SetCallbackType type = SetCallbackType.Set)
    {
        switch (type)
        {
            case SetCallbackType.Set:
                subCallback = callback;
                break;

            case SetCallbackType.Add:
                subCallback += callback;
                break;

            case SetCallbackType.Remove:
                subCallback -= callback;
                break;

            default:
                return;
        }

        callback?.Invoke(value);
    }

    public delegate void PrevCallback(ref T currentValue, T newValue);
    public delegate void SubCallback(T newValue);
}
