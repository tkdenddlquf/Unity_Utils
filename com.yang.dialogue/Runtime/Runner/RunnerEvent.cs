using System.Collections.Generic;

namespace Yang.Dialogue
{
    /// <summary>
    /// Dispatches named dialogue events to registered callbacks fired by Event nodes during a dialogue run.
    /// </summary>
    internal class RunnerEvent
    {
        private readonly Dictionary<string, System.Action> callbacks = new();

        /// <summary>Invokes the callback registered under the given event id, if any.</summary>
        public void OnEvent(string id)
        {
            if (callbacks.TryGetValue(id, out System.Action callback)) callback?.Invoke();
        }

        /// <summary>Removes all registered event callbacks.</summary>
        public void ClearCallbacks() => callbacks.Clear();

        /// <summary>Registers a callback for an event id, ensuring it is subscribed exactly once.</summary>
        public void RegisterCallback(string id, System.Action callback)
        {
            if (callbacks.ContainsKey(id))
            {
                callbacks[id] -= callback;
                callbacks[id] += callback;
            }
            else callbacks.Add(id, callback);
        }

        /// <summary>Unregisters a callback from an event id; returns false if the id was not registered.</summary>
        public bool UnregisterCallback(string id, System.Action callback)
        {
            if (callbacks.ContainsKey(id))
            {
                callbacks[id] -= callback;

                return true;
            }

            return false;
        }
    }
}