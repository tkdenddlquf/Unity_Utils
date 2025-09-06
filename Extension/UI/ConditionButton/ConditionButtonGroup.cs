using System.Collections.Generic;
using UnityEngine;

public class ConditionButtonGroup : MonoBehaviour
{
    private readonly List<ConditionButton> buttons = new();

    public void AddButton(ConditionButton button)
    {
        buttons.Add(button);

        Refresh();
    }

    public void RemoveButton(ConditionButton button)
    {
        buttons.Remove(button);

        Refresh();
    }

    public void Refresh()
    {
        foreach (ConditionButton button in buttons) button.Refresh();
    }
}
