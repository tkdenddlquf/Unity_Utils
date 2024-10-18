using UnityEngine;
using UnityEngine.Events;

public class SpriteEventPC : MonoBehaviour
{
    public UnityEvent OnClick;

    private void OnMouseDown()
    {
        OnClick?.Invoke();
    }
}
