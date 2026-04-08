using System;
using UnityEngine;

namespace Yang.Dialogue
{
    [Serializable]
    public struct RunnerValue
    {
        [SerializeField] private string key;
        public readonly string Key => key;

        [SerializeField] private ValueType type;
        public readonly ValueType Type => type;

        [SerializeField] private float value;

        public RunnerValue(string key, float value)
        {
            this.key = key;

            type = ValueType.Float;

            this.value = value;
        }

        public RunnerValue(string key, bool value)
        {
            this.key = key;

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