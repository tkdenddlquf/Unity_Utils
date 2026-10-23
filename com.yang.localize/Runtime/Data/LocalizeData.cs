using System;
using UnityEngine;

namespace Yang.Localize
{
    [Serializable]
    public class LocalizeData
    {
        [SerializeField] private LocalizeTableType type;
        [SerializeField] private string tableKey = "";
        [SerializeField] private string[] entryKeys = Array.Empty<string>();

#if UNITY_EDITOR
        [SerializeField] private string tableGuid = "";
        [SerializeField] private long[] entryIDs = Array.Empty<long>();
#endif

        public int Length => entryKeys?.Length ?? 0;

        public bool IsEmpty => Length == 0;

        public LocalizeReference this[int index] => new(tableKey, entryKeys[index]);
    }

    public enum LocalizeTableType
    {
        String,
        Asset,
    }

    [Serializable]
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
        public override bool Equals(object obj) => obj is LocalizeReference other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(tableName, entryName);
        public override string ToString() => $"{tableName}/{entryName}";
    }
}
