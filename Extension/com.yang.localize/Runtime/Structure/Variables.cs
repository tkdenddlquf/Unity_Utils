using System;
using System.Collections.Generic;
using UnityEngine.Localization.SmartFormat.Core.Extensions;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Yang.Localize
{
    public class Variables<T> : IVariableValueChanged
    {
        public event Action<IVariable> ValueChanged;

        private readonly List<T> values = new();

        public T this[int index]
        {
            get => values[index];
            set
            {
                values[index] = value;

                ValueChanged?.Invoke(this);
            }
        }

        public void Add(T value)
        {
            values.Add(value);

            ValueChanged?.Invoke(this);
        }

        public bool Remove(T value)
        {
            bool result = values.Remove(value);

            ValueChanged?.Invoke(this);

            return result;
        }

        public void Remove(int index)
        {
            values.RemoveAt(index);

            ValueChanged?.Invoke(this);
        }

        public void Clear()
        {
            values.Clear();

            ValueChanged?.Invoke(this);
        }

        public object GetSourceValue(ISelectorInfo selector) => values;
    }
}
