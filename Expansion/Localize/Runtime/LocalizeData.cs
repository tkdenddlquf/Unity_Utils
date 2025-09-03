using System;

namespace Yang.Localize
{
    [Serializable]
    public class LocalizeData
    {
        public LocalizeTableType type;

        public string tableName;
        public string[] entryNames;

        public LocalizeReference this[int index] => new(tableName, entryNames[index]);
    }

    public enum LocalizeTableType
    {
        String,
        Asset,
    }

    public struct LocalizeReference
    {
        public string tableName;
        public string entryName;

        public LocalizeReference(string tableName, string entryName)
        {
            this.tableName = tableName;
            this.entryName = entryName;
        }
    }
}