using System.Collections.Generic;

namespace Yang.Dialogue
{
    /// <summary>
    /// Stores and mutates the dialogue's named runtime variables, raising change notifications and per-key callbacks.
    /// </summary>
    internal class RunnerTrigger
    {
        /// <summary>Raised with the variable key whenever any stored value is added, changed, or removed.</summary>
        public event System.Action<string> OnAnyValueChanged;

        private readonly Dictionary<string, RunnerValue> values = new();
        private readonly Dictionary<string, System.Action> callbacks = new();

        /// <summary>All currently stored variables.</summary>
        public IReadOnlyCollection<RunnerValue> Values => values.Values;

        /// <summary>Replaces all stored variables with the given set.</summary>
        public void SetDatas(IReadOnlyList<RunnerValue> values)
        {
            ClearValues();

            foreach (RunnerValue value in values) this.values.Add(value.Key, value);
        }

        /// <summary>Removes all stored variables.</summary>
        public void ClearValues() => values.Clear();

        /// <summary>Removes all registered value-change callbacks.</summary>
        public void ClearCallbacks() => callbacks.Clear();

        /// <summary>Returns true if a variable with the given key exists.</summary>
        public bool ContainsKey(string key) => values.ContainsKey(key);

        /// <summary>Removes a variable, firing its callback and the change event; returns false if the key was absent.</summary>
        public bool RemoveValue(string key)
        {
            if (!values.Remove(key)) return false;

            if (callbacks.TryGetValue(key, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(key);

            return true;
        }

        #region Get Set
        /// <summary>Returns the float value for the key, or 0 if the key is missing.</summary>
        public float GetFloatValue(string key)
        {
            if (values.TryGetValue(key, out RunnerValue value)) return value.GetFloatValue();

            return 0;
        }

        /// <summary>Returns the bool value for the key, or false if the key is missing.</summary>
        public bool GetBoolValue(string key)
        {
            if (values.TryGetValue(key, out RunnerValue value)) return value.GetBoolValue();

            return false;
        }

        /// <summary>Sets a float variable, firing its callback and the change event.</summary>
        public void SetValue(string key, float value)
        {
            values[key] = new(key, value);

            if (callbacks.TryGetValue(key, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(key);
        }

        /// <summary>Sets a bool variable, firing its callback and the change event.</summary>
        public void SetValue(string key, bool value)
        {
            values[key] = new(key, value);

            if (callbacks.TryGetValue(key, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(key);
        }
        #endregion

        #region Callback
        /// <summary>Registers a callback fired when the given variable key changes, ensuring it is subscribed once.</summary>
        public void RegisterCallback(string key, System.Action callback)
        {
            if (callbacks.ContainsKey(key))
            {
                callbacks[key] -= callback;
                callbacks[key] += callback;
            }
            else callbacks.Add(key, callback);
        }

        /// <summary>Unregisters a callback from a variable key; returns false if the key had no callbacks.</summary>
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