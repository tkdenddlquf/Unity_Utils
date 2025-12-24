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