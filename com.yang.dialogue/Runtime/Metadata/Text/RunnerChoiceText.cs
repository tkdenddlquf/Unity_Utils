namespace Yang.Dialogue
{
    public readonly struct RunnerChoiceText
    {
        public readonly int portIndex;

        public readonly string table;
        public readonly string entry;

        public readonly bool isValid;

        private readonly RunnerCondition[] conditions;
        public System.Collections.Generic.IReadOnlyList<RunnerCondition> Conditions => conditions;

        public RunnerChoiceText(int portIndex, string table, string entry, bool isValid, RunnerCondition[] conditions)
        {
            this.portIndex = portIndex;

            this.table = table;
            this.entry = entry;

            this.isValid = isValid;

            this.conditions = conditions;
        }
    }

    public readonly struct RunnerCondition
    {
        public readonly string key;

        public readonly bool isValid;

        public readonly ValueType type;

        public readonly ValueCheckType checkType;

        private readonly float value;

        public RunnerCondition(string key, bool isValid, float value, ValueCheckType checkType)
        {
            this.key = key;
            this.isValid = isValid;

            this.value = value;

            this.checkType = checkType;

            type = ValueType.Float;
        }

        public RunnerCondition(string key, bool isValid, bool value)
        {
            this.key = key;
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