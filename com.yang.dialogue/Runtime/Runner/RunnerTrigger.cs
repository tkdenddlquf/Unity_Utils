using System.Collections.Generic;

namespace Yang.Dialogue
{
    /// <summary>
    /// Stores and mutates stable-id dialogue variables, raising change notifications and per-field callbacks.
    /// </summary>
    internal class RunnerTrigger
    {
        /// <summary>Raised with the variable FieldId whenever any stored value is added, changed, or removed.</summary>
        public event System.Action<int> OnAnyValueChanged;

        private readonly Dictionary<int, RunnerValue> values = new();
        private readonly Dictionary<int, System.Action> callbacks = new();

        /// <summary>All currently stored variables.</summary>
        public IReadOnlyCollection<RunnerValue> Values => values.Values;

        /// <summary>Replaces all stored variables with the given set.</summary>
        public void SetDatas(IReadOnlyList<RunnerValue> values)
        {
            ClearValues();

            foreach (RunnerValue value in values) this.values.Add(value.FieldId, value);
        }

        /// <summary>Removes all stored variables.</summary>
        public void ClearValues() => values.Clear();

        /// <summary>Removes all registered value-change callbacks.</summary>
        public void ClearCallbacks() => callbacks.Clear();

        /// <summary>Returns true if a variable with the given FieldId exists.</summary>
        public bool ContainsKey(int fieldId) => values.ContainsKey(fieldId);

        /// <summary>Removes a variable, firing its callback and the change event.</summary>
        public bool RemoveValue(int fieldId)
        {
            if (!values.Remove(fieldId)) return false;

            if (callbacks.TryGetValue(fieldId, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(fieldId);

            return true;
        }

        #region Get Set
        /// <summary>Returns the float value for the FieldId, or 0 if it is missing.</summary>
        public float GetFloatValue(int fieldId)
        {
            if (values.TryGetValue(fieldId, out RunnerValue value)) return value.GetFloatValue();

            return 0;
        }

        /// <summary>Returns the bool value for the FieldId, or false if it is missing.</summary>
        public bool GetBoolValue(int fieldId)
        {
            if (values.TryGetValue(fieldId, out RunnerValue value)) return value.GetBoolValue();

            return false;
        }

        /// <summary>Sets a float variable, firing its callback and the change event.</summary>
        public void SetValue(int fieldId, float value)
        {
            values[fieldId] = new(fieldId, value);

            if (callbacks.TryGetValue(fieldId, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(fieldId);
        }

        /// <summary>Sets a bool variable, firing its callback and the change event.</summary>
        public void SetValue(int fieldId, bool value)
        {
            values[fieldId] = new(fieldId, value);

            if (callbacks.TryGetValue(fieldId, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(fieldId);
        }
        #endregion

        #region Callback
        /// <summary>Registers a callback fired when the given variable FieldId changes.</summary>
        public void RegisterCallback(int fieldId, System.Action callback)
        {
            if (callbacks.ContainsKey(fieldId))
            {
                callbacks[fieldId] -= callback;
                callbacks[fieldId] += callback;
            }
            else callbacks.Add(fieldId, callback);
        }

        /// <summary>Unregisters a callback from a variable FieldId.</summary>
        public bool UnregisterCallback(int fieldId, System.Action callback)
        {
            if (callbacks.ContainsKey(fieldId))
            {
                callbacks[fieldId] -= callback;

                return true;
            }

            return false;
        }
        #endregion
    }
}
