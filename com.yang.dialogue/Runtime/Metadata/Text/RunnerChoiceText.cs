using System.Collections;
using System.Collections.Generic;

namespace Yang.Dialogue
{
    /// <summary>
    /// Reusable, allocation-free view over the choices currently exposed by a Choice node.
    /// Iterate the concrete type directly with foreach to use its struct enumerator without boxing.
    /// </summary>
    public sealed class RunnerChoiceCollection : IReadOnlyList<RunnerChoiceText>
    {
        private readonly RunnerChoiceText[] choices;
        internal RunnerCondition[][] ConditionBuffers { get; }

        public int Count { get; private set; }
        public RunnerChoiceText this[int index] => choices[index];

        internal RunnerChoiceCollection(int capacity)
        {
            choices = new RunnerChoiceText[capacity];
            ConditionBuffers = new RunnerCondition[capacity][];
        }

        internal void Set(int index, RunnerChoiceText choice) => choices[index] = choice;
        internal void SetCount(int count) => Count = count;

        public Enumerator GetEnumerator() => new(this);
        IEnumerator<RunnerChoiceText> IEnumerable<RunnerChoiceText>.GetEnumerator() => new Enumerator(this);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public struct Enumerator : IEnumerator<RunnerChoiceText>
        {
            private readonly RunnerChoiceCollection collection;
            private int index;

            internal Enumerator(RunnerChoiceCollection collection)
            {
                this.collection = collection;
                index = -1;
            }

            public RunnerChoiceText Current => collection.choices[index];
            object IEnumerator.Current => Current;

            public bool MoveNext() => ++index < collection.Count;
            public void Reset() => index = -1;
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Immutable data for a single selectable choice option, passed into the View's choice callback for display and selection.
    /// </summary>
    public readonly struct RunnerChoiceText
    {
        /// <summary>Output port index of this choice; return it from the choice callback to follow this branch.</summary>
        public readonly int portIndex;

        /// <summary>Localization table name key for the choice text. Use with entry to look up the localized string.</summary>
        public readonly string table;

        /// <summary>Localization entry (key) within the table for the choice text. Use with table to look up the localized string.</summary>
        public readonly string entry;

        /// <summary>True if all of this choice's conditions pass; false when conditions fail (display as disabled/greyed out).</summary>
        public readonly bool isValid;

        private readonly RunnerCondition[] conditions;

        /// <summary>The condition checks attached to this choice; inspect to show why a choice is valid or invalid.</summary>
        public System.Collections.Generic.IReadOnlyList<RunnerCondition> Conditions => conditions;

        /// <summary>Creates a choice entry from its port index, Localization keys, validity, and evaluated conditions.</summary>
        public RunnerChoiceText(int portIndex, string table, string entry, bool isValid, RunnerCondition[] conditions)
        {
            this.portIndex = portIndex;

            this.table = table;
            this.entry = entry;

            this.isValid = isValid;

            this.conditions = conditions;
        }
    }

    /// <summary>
    /// Immutable result of a single condition check on a choice, describing the variable, its expected value, and whether it passed.
    /// </summary>
    public readonly struct RunnerCondition
    {
        /// <summary>Stable FieldId of the trigger variable this condition tests.</summary>
        public readonly int fieldId;

        /// <summary>True if this individual condition passed.</summary>
        public readonly bool isValid;

        /// <summary>Whether this condition compares a Float or Bool value.</summary>
        public readonly ValueType type;

        /// <summary>Comparison operator used for the check (relevant for Float conditions).</summary>
        public readonly ValueCheckType checkType;

        private readonly float value;

        /// <summary>Creates a float condition with the compared FieldId and expected value.</summary>
        public RunnerCondition(int fieldId, bool isValid, float value, ValueCheckType checkType)
        {
            this.fieldId = fieldId;
            this.isValid = isValid;

            this.value = value;

            this.checkType = checkType;

            type = ValueType.Float;
        }

        /// <summary>Creates a bool condition with the compared FieldId and expected value.</summary>
        public RunnerCondition(int fieldId, bool isValid, bool value)
        {
            this.fieldId = fieldId;
            this.isValid = isValid;

            this.value = value ? 1 : 0;

            checkType = default;

            type = ValueType.Bool;
        }

        /// <summary>Returns the expected float value, or 0 when this is not a Float condition.</summary>
        public readonly float GetFloatValue()
        {
            if (type == ValueType.Float) return value;

            return 0;
        }

        /// <summary>Returns the expected bool value, or false when this is not a Bool condition.</summary>
        public readonly bool GetBoolValue()
        {
            if (type == ValueType.Bool) return value == 1;

            return false;
        }
    }
}
