using System.Collections.Generic;

namespace Yang.Dialogue
{
    internal class RunnerEvent
    {
        private readonly Dictionary<string, System.Action<RunnerCommand>> callbacks = new();

        public void OnEvent(RunnerCommand data)
        {
            if (callbacks.TryGetValue(data.ID, out System.Action<RunnerCommand> callback)) callback?.Invoke(data);
        }

        public void ClearCallbacks() => callbacks.Clear();

        public void RegisterCallback(string id, System.Action<RunnerCommand> callback)
        {
            if (callbacks.ContainsKey(id))
            {
                callbacks[id] -= callback;
                callbacks[id] += callback;
            }
            else callbacks.Add(id, callback);
        }

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
