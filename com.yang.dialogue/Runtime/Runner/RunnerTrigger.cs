using System.Collections.Generic;

namespace Yang.Dialogue
{
    internal class RunnerTrigger
    {
        public event System.Action<string> OnAnyValueChanged;

        private readonly Dictionary<string, RunnerValue> values = new();
        private readonly Dictionary<string, System.Action> callbacks = new();

        public IReadOnlyCollection<RunnerValue> Values => values.Values;

        public void SetDatas(IReadOnlyList<RunnerValue> values)
        {
            ClearValues();

            foreach (RunnerValue value in values) this.values.Add(value.Key, value);
        }

        public void ClearValues() => values.Clear();

        public void ClearCallbacks() => callbacks.Clear();

        public bool ContainsKey(string key) => values.ContainsKey(key);

        public bool RemoveValue(string key)
        {
            if (callbacks.TryGetValue(key, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(key);

            return values.Remove(key);
        }

        #region Get Set
        public float GetFloatValue(string key)
        {
            if (values.TryGetValue(key, out RunnerValue value)) return value.GetFloatValue();

            return 0;
        }

        public bool GetBoolValue(string key)
        {
            if (values.TryGetValue(key, out RunnerValue value)) return value.GetBoolValue();

            return false;
        }

        public void SetValue(string key, float value)
        {
            values[key] = new(key, value);

            if (callbacks.TryGetValue(key, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(key);
        }

        public void SetValue(string key, bool value)
        {
            values[key] = new(key, value);

            if (callbacks.TryGetValue(key, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(key);
        }
        #endregion

        #region Callback
        public void RegisterCallback(string key, System.Action callback)
        {
            if (callbacks.ContainsKey(key))
            {
                callbacks[key] -= callback;
                callbacks[key] += callback;
            }
            else callbacks.Add(key, callback);
        }

        public bool UnregisterCallback(string key, System.Action callback)
        {
            if (callbacks.ContainsKey(key))
            {
                callbacks[key] -= callback;

                return true;
            }

            return false;
        }
        #endregion
    }
}