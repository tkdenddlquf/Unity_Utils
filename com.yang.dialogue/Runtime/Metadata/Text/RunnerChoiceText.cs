using System.Collections;
using System.Collections.Generic;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue
{
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

    public readonly struct RunnerChoiceText
    {
        public readonly int portIndex;

        public readonly string table;

        public readonly string entry;
        public readonly System.Guid tableGuid;
        public readonly long entryId;
        public TableReference TableReference => tableGuid != System.Guid.Empty ? (TableReference)tableGuid : (TableReference)table;
        public TableEntryReference TableEntryReference => entryId != 0 ? (TableEntryReference)entryId : (TableEntryReference)entry;

        public readonly bool isValid;

        private readonly RunnerCondition[] conditions;

        public System.Collections.Generic.IReadOnlyList<RunnerCondition> Conditions => conditions;

        public RunnerChoiceText(int portIndex, string table, string entry, bool isValid, RunnerCondition[] conditions)
            : this(portIndex, table, entry, System.Guid.Empty, 0, isValid, conditions) { }
        public RunnerChoiceText(int portIndex, string table, string entry, System.Guid tableGuid, long entryId, bool isValid, RunnerCondition[] conditions)
        {
            this.portIndex = portIndex;
            this.table = table;
            this.entry = entry;
            this.tableGuid = tableGuid;
            this.entryId = entryId;
            this.isValid = isValid;
            this.conditions = conditions;
        }
    }

    public readonly struct RunnerCondition
    {
        public readonly int fieldId;

        public readonly bool isValid;

        public readonly ValueType type;

        public readonly ValueCheckType checkType;

        private readonly float value;

        public RunnerCondition(int fieldId, bool isValid, float value, ValueCheckType checkType)
        {
            this.fieldId = fieldId;
            this.isValid = isValid;
            this.value = value;
            this.checkType = checkType;
            type = ValueType.Float;
        }

        public RunnerCondition(int fieldId, bool isValid, bool value)
        {
            this.fieldId = fieldId;
            this.isValid = isValid;
            this.value = value ? 1 : 0;
            checkType = default;
            type = ValueType.Bool;
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
