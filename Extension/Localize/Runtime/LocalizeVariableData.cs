using System;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Yang.Localize
{
    public readonly struct LocalizeVariableData : IEquatable<LocalizeVariableData>
    {
        public readonly LocalizeReference reference;

        public readonly string key;
        public readonly IVariable value;

        public LocalizeVariableData(LocalizeReference reference, string key, IVariable value)
        {
            this.reference = reference;

            this.key = key;
            this.value = value;
        }

        public bool Equals(LocalizeVariableData other) => reference.Equals(other.reference) && key == other.key && value == other.value;

        public override int GetHashCode() => HashCode.Combine(reference, key, value);
    }
}