using System;
using UnityEngine;

namespace Yang.Dialogue
{
    [Serializable]
    public struct RunnerValue
    {
        private enum ValueType : byte
        {
            Float,
            Bool,
        }

        public enum SetterType : byte
        {
            Plus,
            Minus,
            Set,
        }

        public enum CheckType : byte
        {
            Less,
            Equal,
            LessEqual,
            Greater,
            NotEqual,
            GreaterEqual,
        }

        [SerializeField] private string key;
        public readonly string Key => key;

        [SerializeField] private ValueType type;

        [SerializeField] private float floatValue;
        [SerializeField] private bool boolValue;

        public RunnerValue(string key, float value)
        {
            this = default;

            this.key = key;

            type = ValueType.Float;

            floatValue = value;
        }

        public RunnerValue(string key, bool value)
        {
            this = default;

            this.key = key;

            type = ValueType.Bool;

            boolValue = value;
        }

        public readonly float GetFloatValue()
        {
            if (type == ValueType.Float) return floatValue;

            return 0;
        }

        public readonly bool GetBoolValue()
        {
            if (type == ValueType.Bool) return boolValue;

            return false;
        }
    }
}