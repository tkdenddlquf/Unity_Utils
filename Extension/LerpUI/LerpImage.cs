using System;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class LerpImage
{
    public Image image;
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

        if (image.fillAmount < Data) increase = true;
        else increase = false;

        action.Add(FixedUpdate);
    }

    public void FixedUpdate()
    {
        lerpValue = Mathf.Lerp(image.fillAmount, Data, speed);

        image.fillAmount = lerpValue;

        if (increase ? image.fillAmount > Data - 0.0001f : image.fillAmount < Data + 0.0001f) // 증가
        {
            lerpValue = Data;

            action.Remove(FixedUpdate);
            callback?.Invoke(Data, Data);
        }
        else callback?.Invoke(image.fillAmount, Data);
    }
}
