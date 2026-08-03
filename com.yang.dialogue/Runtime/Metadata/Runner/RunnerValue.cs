using System;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// Serializable stable-id runtime variable holding either a float or bool.
    /// </summary>
    [Serializable]
    public struct RunnerValue
    {
        [SerializeField] private int fieldId;

        /// <summary>Stable schema id identifying this variable.</summary>
        public readonly int FieldId => fieldId;

        [SerializeField] private ValueType type;

        /// <summary>Whether this variable stores a Float or Bool value.</summary>
        public readonly ValueType Type => type;

        [SerializeField] private float value;

        /// <summary>Creates a float-typed variable with the given FieldId and value.</summary>
        public RunnerValue(int fieldId, float value)
        {
            this.fieldId = fieldId;

            type = ValueType.Float;

            this.value = value;
        }

        /// <summary>Creates a bool-typed variable with the given FieldId and value.</summary>
        public RunnerValue(int fieldId, bool value)
        {
            this.fieldId = fieldId;

            type = ValueType.Bool;

            this.value = value ? 1 : 0;
        }

        /// <summary>Returns the stored float value, or 0 when this variable is not Float-typed.</summary>
        public readonly float GetFloatValue()
        {
            if (type == ValueType.Float) return value;

            return 0;
        }

        /// <summary>Returns the stored bool value, or false when this variable is not Bool-typed.</summary>
        public readonly bool GetBoolValue()
        {
            if (type == ValueType.Bool) return value == 1;

            return false;
        }
    }
}
