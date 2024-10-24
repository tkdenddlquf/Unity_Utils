using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LerpScrollbar
{
    public Scrollbar scrollbar;
    public float speed = 0.2f;

    public LerpAction action;

    private bool increase;
    private float lerpValue;

    public Action<float, float> callback;
    public float Data { get; private set; }

    public void SetData(float _value)
    {
        Data = _value;

        if (scrollbar.value < Data) increase = true;
        else increase = false;

        action.Add(FixedUpdate);
    }

    public void FixedUpdate()
    {
        lerpValue = Mathf.Lerp(scrollbar.value, Data, speed);

        scrollbar.value = lerpValue;

        if (increase ? scrollbar.value > Data - 0.0001f : scrollbar.value < Data + 0.0001f)
        {
            lerpValue = Data;

            action.Remove(FixedUpdate);
            callback?.Invoke(Data, Data);
        }
        else callback?.Invoke(scrollbar.value, Data);
    }
}