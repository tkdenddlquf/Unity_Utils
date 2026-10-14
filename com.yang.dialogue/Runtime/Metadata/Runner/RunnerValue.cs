using System;
using UnityEngine;

namespace Yang.Dialogue
{
    [Serializable]
    public struct RunnerValue
    {
        [SerializeField] private int fieldId;

        public readonly int FieldId => fieldId;

        [SerializeField] private ValueType type;

        public readonly ValueType Type => type;

        [SerializeField] private float value;

        public RunnerValue(int fieldId, float value)
        {
            this.fieldId = fieldId;
            type = ValueType.Float;
            this.value = value;
        }

        public RunnerValue(int fieldId, bool value)
        {
            this.fieldId = fieldId;
            type = ValueType.Bool;
            this.value = value ? 1 : 0;
        }

        public readonly float GetFloatValue()
        {
            if (type == ValueType.Float) return value;
            return 0;
        }

        public readonly bool GetBoolValue()
        {
            if (type == ValueType.Bool) return value == 1;
            return false;
        }
    }
}
