using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue.Editor
{
    public readonly struct EntryData : IEquatable<EntryData>
    {
        public readonly long id;
        public readonly string key;

        public readonly IReadOnlyCollection<LazyLoadReference<LocalizationTable>> tables;

        public EntryData(long id, string key, IReadOnlyCollection<LazyLoadReference<LocalizationTable>> tables)
        {
            this.id = id;
            this.key = key;
            this.tables = tables;
        }

        public EntryData(long id, string key)
        {
            this.id = id;
            this.key = key;
            tables = null;
        }

        public readonly string GetText(LocaleIdentifier identifier)
        {
            if (tables == null) return "";

            foreach (LazyLoadReference<LocalizationTable> reference in tables)
            {
                LocalizationTable table = reference.asset;

                if (table.LocaleIdentifier == identifier && table is StringTable stringTable)
                {
                    StringTableEntry entry = id != 0 && stringTable.SharedData.GetEntry(id) != null ? stringTable.GetEntry(id) : stringTable.GetEntry(key);

                    if (entry == null) return "";
                    else return entry.Value;
                }
            }

            return "";
        }

        public override string ToString() => key;

        public readonly bool Equals(EntryData other) => id != 0 || other.id != 0 ? id != 0 && id == other.id : key == other.key;

        public readonly override bool Equals(object obj) => obj is EntryData other && Equals(other);

        public readonly override int GetHashCode() => id != 0 ? id.GetHashCode() : (key?.GetHashCode() ?? 0);
    }
}
