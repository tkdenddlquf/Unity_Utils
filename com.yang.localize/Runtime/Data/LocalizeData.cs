using System;
using UnityEngine;

namespace Yang.Localize
{
    [Serializable]
    public class LocalizeData
    {
        [SerializeField] private LocalizeTableType type;

        [SerializeField] private string tableKey;
        [SerializeField] private string[] entryKeys;

        public string Key => tableKey;

        public int Length => entryKeys.Length;

        public LocalizeReference this[int index] => new(tableKey, entryKeys[index]);

#if UNITY_EDITOR
        [SerializeField] private string tableGuid;
        [SerializeField] private long[] entryIDs;
#endif
    }

    public enum LocalizeTableType
    {
        String,
        Asset,
    }

    public readonly struct LocalizeReference : IEquatable<LocalizeReference>
    {
        public readonly string tableName;
        public readonly string entryName;

        public LocalizeReference(string tableName, string entryName)
        {
            this.tableName = tableName;
            this.entryName = entryName;
        }

        public bool Equals(LocalizeReference other) => tableName == other.tableName && entryName == other.entryName;

        public override int GetHashCode() => HashCode.Combine(tableName, entryName);
    }
}