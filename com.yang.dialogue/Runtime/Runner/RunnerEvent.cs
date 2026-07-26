using System.Collections.Generic;

namespace Yang.Dialogue
{
    /// <summary>
    /// Dispatches named dialogue events to registered callbacks fired by Event nodes during a dialogue run.
    /// </summary>
    internal class RunnerEvent
    {
        private readonly Dictionary<string, System.Action<RunnerCommand>> callbacks = new();

        /// <summary>Invokes the callback registered under the given event id, if any.</summary>
        public void OnEvent(RunnerCommand data)
        {
            if (callbacks.TryGetValue(data.ID, out System.Action<RunnerCommand> callback)) callback?.Invoke(data);
        }

        /// <summary>Removes all registered event callbacks.</summary>
        public void ClearCallbacks() => callbacks.Clear();

        /// <summary>Registers a callback for an event id, ensuring it is subscribed exactly once.</summary>
        public void RegisterCallback(string id, System.Action<RunnerCommand> callback)
        {
            if (callbacks.ContainsKey(id))
            {
                callbacks[id] -= callback;
                callbacks[id] += callback;
            }
            else callbacks.Add(id, callback);
        }

        /// <summary>Unregisters a callback from an event id; returns false if the id was not registered.</summary>
        public bool UnregisterCallback(string id, System.Action<RunnerCommand> callback)
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
