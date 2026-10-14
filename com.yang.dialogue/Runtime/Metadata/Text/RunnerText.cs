using UnityEngine.Localization.Tables;

namespace Yang.Dialogue
{
    public readonly struct RunnerText
    {
        public readonly string table;

        public readonly string entry;
        public readonly System.Guid tableGuid;
        public readonly long entryId;
        public TableReference TableReference => tableGuid != System.Guid.Empty ? (TableReference)tableGuid : (TableReference)table;
        public TableEntryReference TableEntryReference => entryId != 0 ? (TableEntryReference)entryId : (TableEntryReference)entry;

        public RunnerText(string table, string entry) : this(table, entry, System.Guid.Empty, 0) { }
        public RunnerText(string table, string entry, System.Guid tableGuid, long entryId)
        {
            this.table = table;
            this.entry = entry;
            this.tableGuid = tableGuid;
            this.entryId = entryId;
        }
    }
}
