using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Localize.Editor
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

        public static int GetTableIndex(this IReadOnlyList<LocalizationTableCollection> collections, string value, string stringGuid)
        {
            System.Guid.TryParse(stringGuid, out System.Guid guid);

            for (int i = 0; i < collections.Count; i++)
            {
                LocalizationTableCollection collection = collections[i];

                if (collection.TableCollectionNameReference.TableCollectionNameGuid == guid) return i;
            }

            for (int i = 0; i < collections.Count; i++)
            {
                LocalizationTableCollection collection = collections[i];

                if (collection.TableCollectionName == value) return i;
            }

            return -1;
        }
    }
}