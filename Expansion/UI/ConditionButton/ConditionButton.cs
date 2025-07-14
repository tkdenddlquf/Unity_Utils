using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ConditionButton : Selectable, IPointerClickHandler, ISubmitHandler
{
    [Header("Condition")]
    [SerializeField] private bool changeInteractable;
    [SerializeField] private Sprite onSprite;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private ConditionButtonGroup group;

    public bool IsOn { get; private set; }

    private Func<bool, bool> onCheck;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (group != null) group.AddButton(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (group != null) group.RemoveButton(this);
    }

    public void AddCheck(Func<bool, bool> check)
    {
        onCheck -= check;
        onCheck += check;

        Refresh();
    }

    public void RemoveCheck(Func<bool, bool> check)
    {
        onCheck -= check;

        Refresh();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        Press();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Press();

        if (!IsActive() || !IsInteractable()) return;

        DoStateTransition(SelectionState.Pressed, false);
        StartCoroutine(OnFinishSubmit());
    }

    public void Refresh() => CheckState(true);

    private void CheckState(bool refresh)
    {
        bool? check = onCheck?.Invoke(refresh);

        IsOn = check != null && check.Value;

        if (IsOn)
        {
            if (onSprite != null) image.sprite = onSprite;
            if (changeInteractable) interactable = true;
        }
        else
        {
            if (offSprite != null) image.sprite = offSprite;
            if (changeInteractable) interactable = false;
        }
    }

    private void Press()
    {
        if (!IsActive() || !IsInteractable()) return;

        UISystemProfilerApi.AddMarker("ConditionButton.CheckState", this);

        CheckState(false);

        if (group != null) group.Refresh();
    }

    private IEnumerator OnFinishSubmit()
    {
        var fadeTime = colors.fadeDuration;
        var elapsedTime = 0f;

        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        DoStateTransition(currentSelectionState, false);
    }
}
