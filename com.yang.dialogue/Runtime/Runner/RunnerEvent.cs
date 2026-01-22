using System.Collections.Generic;
using UnityEngine;

public class RunnerEvent
{
    private readonly Dictionary<string, System.Action<bool>> callbacks = new();

    public void OnEvent(string id)
    {
        if (callbacks.TryGetValue(id, out System.Action<bool> callback)) callback?.Invoke(true);
    }

    public void RegisterCallback(string id, System.Action<bool> callback)
    {
        if (callbacks.ContainsKey(id))
        {
            callbacks[id] -= callback;
            callbacks[id] += callback;
        }
        else callbacks.Add(id, callback);
    }

    public bool UnregisterCallback(string id, System.Action<bool> callback)
    {
        if (callbacks.ContainsKey(id))
        {
            callbacks[id] -= callback;

            return true;
        }

        return false;
    }
}
