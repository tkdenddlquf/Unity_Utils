using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Extension helpers that populate lists of table names and entries from localization collections.
    /// </summary>
    public static class LocalizeSetter
    {
        /// <summary>Fills the list with each collection's display name, prefixed by its group when present.</summary>
        public static void SetTables(this IReadOnlyList<LocalizationTableCollection> collections, List<string> tables)
        {
            tables.Clear();

            if (collections == null) return;

            foreach (LocalizationTableCollection collection in collections)
            {
                string tableName = collection.TableCollectionName;
                string group = collection.Group;

                tables.Add(string.IsNullOrEmpty(group) ? tableName : $"{group}/{tableName}");
            }
        }

        /// <summary>Fills the list with one <see cref="EntryData"/> per shared entry in the collection.</summary>
        public static void SetEntries(this LocalizationTableCollection collection, List<EntryData> entries)
        {
            entries.Clear();

            if (collection == null) return;

            foreach (SharedTableData.SharedTableEntry current in collection.SharedData.Entries)
            {
                EntryData data = new(current.Id, current.Key, collection.Tables);

                entries.Add(data);
            }
        }
    }
}