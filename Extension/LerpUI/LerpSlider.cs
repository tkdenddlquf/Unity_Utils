using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LerpSlider
{
    public Slider slider;
    public float speed = 0.2f;

    private bool increase;
    private float lerpValue;
    private LerpUIAction action;

    public Action<float> callback;
    public float Data { get; private set; }

    public void SetData(LerpUIAction _action, float _value)
    {
        action = _action;
        Data = _value;

        if (slider.value < Data) increase = true;
        else increase = false;

        action.Add(FixedUpdate);
    }

    public void FixedUpdate()
    {
        lerpValue = Mathf.Lerp(slider.value, Data, speed);

        slider.value = lerpValue;

        if (increase ? slider.value > Data - 0.0001f : slider.value < Data + 0.0001f)
        {
            lerpValue = Data;

            action.Remove(FixedUpdate);
            callback?.Invoke(Data, Data);
        }
        else callback?.Invoke(slider.value, Data);
    }
}