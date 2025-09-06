using System;
using UnityEngine;

namespace Yang.Localize
{
    [Serializable]
    public class LocalizeData
    {
        [SerializeField] private LocalizeTableType type;

        [SerializeField] private string tableName;
        [SerializeField] private string[] entryNames;

        public string Name => tableName;

        public int Length => entryNames.Length;

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