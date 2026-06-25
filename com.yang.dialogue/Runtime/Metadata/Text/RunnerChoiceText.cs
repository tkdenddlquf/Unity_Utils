namespace Yang.Dialogue
{
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
        /// <summary>Name of the trigger variable this condition tests.</summary>
        public readonly string key;

        /// <summary>True if this individual condition passed.</summary>
        public readonly bool isValid;

        /// <summary>Whether this condition compares a Float or Bool value.</summary>
        public readonly ValueType type;

        /// <summary>Comparison operator used for the check (relevant for Float conditions).</summary>
        public readonly ValueCheckType checkType;

        private readonly float value;

        /// <summary>Creates a float condition with the compared key, pass result, expected value, and comparison operator.</summary>
        public RunnerCondition(string key, bool isValid, float value, ValueCheckType checkType)
        {
            this.key = key;
            this.isValid = isValid;

            this.value = value;

            this.checkType = checkType;

            type = ValueType.Float;
        }

        /// <summary>Creates a bool condition with the compared key, pass result, and expected boolean value.</summary>
        public RunnerCondition(string key, bool isValid, bool value)
        {
            this.key = key;
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