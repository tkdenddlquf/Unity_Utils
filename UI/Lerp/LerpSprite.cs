using System;
using UnityEngine;

[Serializable]
public class LerpSprite
{
    public SpriteRenderer sprite;
    public int maxSize;
    public float speed = 0.2f;

    private bool increase;
    private float lerpValue;
    private LerpAction action;

    public Action<float, float> callback;
    public float Data { get; private set; }

    public void SetData(LerpAction _action, float _value)
    {
        action = _action;
        Data = _value * maxSize;

        if (sprite.size.x < Data) increase = true;
        else increase = false;

        action.Add(FixedUpdate);
    }

    public void FixedUpdate()
    {
        lerpValue = Mathf.Lerp(sprite.size.x, Data, speed);

        sprite.size = new(lerpValue, sprite.size.y);

        if (increase ? sprite.size.x > Data - 0.0001f : sprite.size.x < Data + 0.0001f)
        {
            lerpValue = Data;

            action.Remove(FixedUpdate);
            callback?.Invoke(Data, Data);
        }
        else callback?.Invoke(sprite.size.x, Data);
    }
}