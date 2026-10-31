using System;
using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue.Editor
{
    public static class LocalizeSetter
    {
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

        public static void SetTableIds(this IReadOnlyList<LocalizationTableCollection> collections, List<Guid> ids)
        {
            ids.Clear();
            ids.Add(Guid.Empty);

            foreach (LocalizationTableCollection collection in collections) ids.Add(collection.TableCollectionNameReference.TableCollectionNameGuid);
        }

        public static LocalizationTableCollection ResolveCollection(this IReadOnlyList<LocalizationTableCollection> collections, string name, Guid guid, long entryId = 0, string entryKey = null)
        {
            foreach (LocalizationTableCollection collection in collections)

                if (guid != Guid.Empty && collection.TableCollectionNameReference.TableCollectionNameGuid == guid) return collection;

            foreach (LocalizationTableCollection collection in collections)

                if (!string.IsNullOrEmpty(name) && collection.TableCollectionName == name) return collection;

            LocalizationTableCollection match = FindUniqueEntryCollection(collections, entryId, null);

            return match ?? FindUniqueEntryCollection(collections, 0, entryKey);
        }

        private static LocalizationTableCollection FindUniqueEntryCollection(IReadOnlyList<LocalizationTableCollection> collections, long id, string key)
        {
            if (id == 0 && string.IsNullOrEmpty(key)) return null;

            LocalizationTableCollection match = null;

            foreach (LocalizationTableCollection collection in collections)
            {
                if (id != 0 ? collection.SharedData.GetEntry(id) == null : collection.SharedData.GetEntry(key) == null) continue;

                if (match != null) return null;

                match = collection;
            }

            return match;
        }

        public static SharedTableData.SharedTableEntry ResolveEntry(this LocalizationTableCollection collection, long id, string key)
        {
            SharedTableData.SharedTableEntry entry = id != 0 ? collection.SharedData.GetEntry(id) : null;

            return entry ?? (string.IsNullOrEmpty(key) ? null : collection.SharedData.GetEntry(key));
        }

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
