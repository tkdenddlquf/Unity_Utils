namespace Yang.Dialogue
{
    /// <summary>
    /// Immutable Localization reference for a single line of dialogue text (speaker name or body), passed into View callbacks.
    /// </summary>
    public readonly struct RunnerText
    {
        /// <summary>Localization table name key for this text. Use with entry to look up the localized string.</summary>
        public readonly string table;

        /// <summary>Localization entry (key) within the table for this text. Use with table to look up the localized string.</summary>
        public readonly string entry;

        /// <summary>Creates a RunnerText from the given Localization table and entry keys.</summary>
        public RunnerText(string table, string entry)
        {
            this.table = table;
            this.entry = entry;
        }
    }
}
