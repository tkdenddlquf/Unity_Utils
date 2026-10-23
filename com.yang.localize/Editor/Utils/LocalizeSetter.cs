using System;
using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEngine;
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

        public static void SetEntries(this LocalizationTableCollection collection, List<EntryData> entries, SystemLanguage previewLanguage)
        {
            entries.Clear();

            if (collection == null) return;

            StringTable previewTable = GetPreviewTable(collection, previewLanguage);

            foreach (SharedTableData.SharedTableEntry current in collection.SharedData.Entries)
            {
                string preview = previewTable != null ? previewTable.GetEntry(current.Id)?.Value : null ?? "";

                entries.Add(new EntryData(current.Id, current.Key, preview));
            }

            entries.Sort((a, b) => string.Compare(a.key, b.key, StringComparison.OrdinalIgnoreCase));
        }

        public static int GetTableIndex(this IReadOnlyList<LocalizationTableCollection> collections, string value, string stringGuid)
        {
            if (collections == null) return -1;

            Guid.TryParse(stringGuid, out Guid guid);

            if (guid != Guid.Empty)
            {
                for (int i = 0; i < collections.Count; i++)
                {
                    if (collections[i].TableCollectionNameReference.TableCollectionNameGuid == guid) return i;
                }
            }

            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < collections.Count; i++)
                {
                    if (collections[i].TableCollectionName == value) return i;
                }
            }

            return -1;
        }

        private static StringTable GetPreviewTable(LocalizationTableCollection collection, SystemLanguage previewLanguage)
        {
            StringTable fallback = null;

            foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
            {
                if (reference.asset is not StringTable table) continue;

                fallback ??= table;

                if (table.LocaleIdentifier == previewLanguage) return table;
            }

            return fallback;
        }
    }
}
