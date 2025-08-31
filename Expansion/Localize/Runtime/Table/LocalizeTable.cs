using System;

namespace Yang.Localize
{
    [Serializable]
    public class LocalizeTable
    {
        public LocalizeTableType type;
        public string tableName;

        public static implicit operator string(LocalizeTable table) => table.tableName;
    }

    public enum LocalizeTableType
    {
        Asset,
        String,
    }
}