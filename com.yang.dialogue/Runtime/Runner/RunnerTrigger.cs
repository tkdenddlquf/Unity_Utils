using System.Collections.Generic;

namespace Yang.Dialogue
{
    internal class RunnerTrigger
    {
        public event System.Action<int> OnAnyValueChanged;

        private readonly Dictionary<int, RunnerValue> values = new();
        private readonly Dictionary<int, System.Action> callbacks = new();

        public IReadOnlyCollection<RunnerValue> Values => values.Values;

        public void SetDatas(IReadOnlyList<RunnerValue> values)
        {
            ClearValues();

            foreach (RunnerValue value in values) this.values.Add(value.FieldId, value);
        }

        public void ClearValues() => values.Clear();

        public void ClearCallbacks() => callbacks.Clear();

        public bool ContainsKey(int fieldId) => values.ContainsKey(fieldId);

        public bool RemoveValue(int fieldId)
        {
            if (!values.Remove(fieldId)) return false;

            if (callbacks.TryGetValue(fieldId, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(fieldId);
            return true;
        }

        #region Get Set
        public float GetFloatValue(int fieldId)
        {
            if (values.TryGetValue(fieldId, out RunnerValue value)) return value.GetFloatValue();

            return 0;
        }

        public bool GetBoolValue(int fieldId)
        {
            if (values.TryGetValue(fieldId, out RunnerValue value)) return value.GetBoolValue();

            return false;
        }

        public void SetValue(int fieldId, float value)
        {
            values[fieldId] = new(fieldId, value);

            if (callbacks.TryGetValue(fieldId, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(fieldId);
        }

        public void SetValue(int fieldId, bool value)
        {
            values[fieldId] = new(fieldId, value);

            if (callbacks.TryGetValue(fieldId, out System.Action callback)) callback?.Invoke();

            OnAnyValueChanged?.Invoke(fieldId);
        }
        #endregion

        #region Callback
        public void RegisterCallback(int fieldId, System.Action callback)
        {
            if (callbacks.ContainsKey(fieldId))
            {
                callbacks[fieldId] -= callback;
                callbacks[fieldId] += callback;
            }
            else callbacks.Add(fieldId, callback);
        }

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
