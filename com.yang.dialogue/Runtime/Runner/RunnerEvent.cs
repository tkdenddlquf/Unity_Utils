using System.Collections.Generic;

namespace Yang.Dialogue
{
    internal class RunnerEvent
    {
        private readonly Dictionary<string, System.Action> callbacks = new();

        public void OnEvent(string id)
        {
            if (callbacks.TryGetValue(id, out System.Action callback)) callback?.Invoke();
        }

        public void ClearCallbacks() => callbacks.Clear();

        public void RegisterCallback(string id, System.Action callback)
        {
            if (callbacks.ContainsKey(id))
            {
                callbacks[id] -= callback;
                callbacks[id] += callback;
            }
            else callbacks.Add(id, callback);
        }

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