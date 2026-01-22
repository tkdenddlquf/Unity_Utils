using System.Collections.Generic;
using UnityEngine;

public class RunnerTrigger
{
    private readonly HashSet<string> triggers = new();
    public IReadOnlyCollection<string> Triggers => triggers;

    private readonly Dictionary<string, System.Action<bool>> callbacks = new();

    public void SetDatas(List<string> triggers)
    {
        this.triggers.Clear();

        foreach (string trigger in triggers) this.triggers.Add(trigger);
    }

    public bool IsTrigger(string trigger) => triggers.Contains(trigger);

    public void SetTrigger(string trigger)
    {
        triggers.Add(trigger);

        if (callbacks.TryGetValue(trigger, out System.Action<bool> callback)) callback?.Invoke(true);
    }

    public bool UnsetTrigger(string trigger)
    {
        if (callbacks.TryGetValue(trigger, out System.Action<bool> callback)) callback?.Invoke(false);

        return triggers.Remove(trigger);
    }

    public void RegisterCallback(string trigger, System.Action<bool> callback)
    {
        if (callbacks.ContainsKey(trigger))
        {
            callbacks[trigger] -= callback;
            callbacks[trigger] += callback;
        }
        else callbacks.Add(trigger, callback);
    }

    public bool UnregisterCallback(string trigger, System.Action<bool> callback)
    {
        if (callbacks.ContainsKey(trigger))
        {
            callbacks[trigger] -= callback;

            return true;
        }

        return false;
    }
}
