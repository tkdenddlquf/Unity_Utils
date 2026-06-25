using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Immutable handle to a single localization entry (id + key + its tables) that can resolve text per locale.
    /// </summary>
    public readonly struct EntryData : IEquatable<EntryData>
    {
        public readonly long id;
        public readonly string key;

        public readonly IReadOnlyCollection<LazyLoadReference<LocalizationTable>> tables;

        /// <summary>Creates an entry bound to the given tables so its text can be looked up per locale.</summary>
        public EntryData(long id, string key, IReadOnlyCollection<LazyLoadReference<LocalizationTable>> tables)
        {
            this.id = id;
            this.key = key;

            this.tables = tables;
        }

        /// <summary>Creates an entry with no backing tables; text lookups will return empty.</summary>
        public EntryData(long id, string key)
        {
            this.id = id;
            this.key = key;

            tables = null;
        }

        /// <summary>Returns this entry's text for the given locale, or empty if not found.</summary>
        public readonly string GetText(LocaleIdentifier identifier)
        {
            if (tables == null) return "";

            foreach (LazyLoadReference<LocalizationTable> reference in tables)
            {
                LocalizationTable table = reference.asset;

                if (table.LocaleIdentifier == identifier && table is StringTable stringTable)
                {
                    StringTableEntry entry = stringTable.GetEntry(key);

                    if (entry == null) return "";
                    else return entry.Value;
                }
            }

            return "";
        }

        /// <summary>Returns the entry key as its string representation.</summary>
        public override string ToString() => key;

        /// <summary>Returns true when both entries share the same key and id.</summary>
        public readonly bool Equals(EntryData other) => key == other.key && id == other.id;

        /// <summary>Returns true when the object is an equal <see cref="EntryData"/>.</summary>
        public readonly override bool Equals(object obj) => obj is EntryData other && Equals(other);

        /// <summary>Returns a hash combining the key and id.</summary>
        public readonly override int GetHashCode() => HashCode.Combine(key, id);
    }
}