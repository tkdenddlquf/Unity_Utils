using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue.Editor
{
    public sealed class DialogueCsvImportOptions
    {
        public bool createTables;
        public bool createEntries;
        public bool createLocaleTables;
        public bool createNodes;
        public readonly HashSet<string> approvedNewNodeIds = new(StringComparer.Ordinal);
        public bool useDefaultTables;
        public bool keepStart;
        public string tableFolder = "Assets";
    }

    internal sealed class DialogueCsvIssue
    {
        public int row;
        public int column;
        public int line;
        public string title;
        public string detail;
        public bool warning;
        public int alternateRow = -1;
        public string alternateValue;
        public string referenceScope;
        public string referenceGroup;
        public string referenceLabel;
    }

    internal sealed class DialogueCsvConflictChoice
    {
        public string value;
        public int count;
        public int firstLine;
    }

    internal sealed class DialogueCsvImportReview
    {
        private readonly DialogueSO asset;
        private readonly Dictionary<(string table, long id), (string key, int row)> entryIds = new();
        private readonly Dictionary<(string table, string key), (long id, int row)> entryKeys = new();
        private readonly Dictionary<(string table, string entry, string locale), (string text, int row)> translations = new();
        private readonly HashSet<string> newTableNames = new();
        private readonly HashSet<string> newEntryNames = new();
        private readonly HashSet<string> renameNames = new();
        private readonly HashSet<string> tableRenameNames = new();
        private readonly Dictionary<Guid, (string name, int row)> requestedTableNames = new();
        private readonly HashSet<string> changedTranslations = new();
        private readonly Dictionary<string, StringTableCollection> tables = new();
        private readonly Dictionary<string, int> columns = new();
        private readonly Dictionary<string, int> ids = new();
        private readonly Dictionary<(bool isEvent, string schemaId), HashSet<int>> instructionFields = new();
        private readonly List<int> lines = new();
        private readonly List<LocalizationReference> localizationReferences = new();
        private readonly Dictionary<string, List<int>> affectedRowsCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<(int row, int column)>> conflictCellsCache = new(StringComparer.Ordinal);

        public readonly DialogueCsvImportOptions options = new();
        public readonly List<DialogueCsvIssue> issues = new();
        public readonly List<List<string>> rows = new();
        public readonly List<string> nodeLabels = new();
        public readonly List<string> tableNames = new();
        public IReadOnlyCollection<string> plannedTables => newTableNames;
        public IReadOnlyCollection<string> plannedEntries => newEntryNames;
        public IReadOnlyCollection<string> plannedRenames => renameNames;
        public IReadOnlyCollection<string> plannedTableRenames => tableRenameNames;
        public IReadOnlyCollection<string> plannedTranslations => changedTranslations;
        public string csv;
        public int nodeCount;
        public int detailCount;
        public int linkCount;
        public int currentNodes;
        public int currentLinks;
        public int newTableCount;
        public int newEntryCount;
        public int renameCount;
        public int tableRenameCount;
        public int translationCount;
        public bool parsed;
        public int errorCount;
        public int warningCount;

        public DialogueCsvImportReview(DialogueSO so, string source)
        {
            asset = so;
            csv = source;
            currentNodes = so.EditorNodes.Count + 1;
            currentLinks = so.EditorLinks.Count;
            Scan();
        }

        public string GetCell(int row, int column)
        {
            if (row < 0 || row >= rows.Count || column < 0 || column >= rows[row].Count) return "";

            return rows[row][column];
        }

        public int GetMissingNodeIdCount()
        {
            if (!parsed || GetColumnName(0) != "ID" || GetColumnName(1) != "Type") return 0;

            int count = 0;

            for (int row = 1; row < rows.Count; row++)
            {
                string type = GetCell(row, 1).Trim();

                if (type != "Start" && DialogueCsvImporter.IsNodeType(type) && string.IsNullOrWhiteSpace(GetCell(row, 0))) count++;
            }

            return count;
        }

        public int GetUnapprovedNodeIdCount()
        {
            int count = 0;

            foreach (DialogueCsvIssue issue in issues)
            {
                if (issue.title == "New node ID") count++;
            }

            return count;
        }

        public int GenerateMissingNodeIds()
        {
            int count = GetMissingNodeIdCount();

            if (count == 0) return 0;

            HashSet<string> used = new(StringComparer.Ordinal);
            used.Add(asset.EditorStartNode.guid);

            foreach (NodeData node in asset.EditorNodes) used.Add(node.guid);

            for (int row = 1; row < rows.Count; row++)
            {
                string id = GetCell(row, 0).Trim();

                if (!string.IsNullOrEmpty(id)) used.Add(id);
            }

            for (int row = 1; row < rows.Count; row++)
            {
                string type = GetCell(row, 1).Trim();

                if (type == "Start" || !DialogueCsvImporter.IsNodeType(type) || !string.IsNullOrWhiteSpace(GetCell(row, 0))) continue;

                string id;

                do id = Guid.NewGuid().ToString();
                while (!used.Add(id));

                SetCellValue(row, 0, id);
            }

            SaveRows();

            return count;
        }

        public (int tableGuids, int entryIds) GetCompletableLocalizationCounts()
        {
            List<(int row, int column, string value)> tableGuids = new();
            List<(int row, int column, string value)> entryIds = new();
            CollectExistingLocalizationReferences(tableGuids, entryIds);

            return (tableGuids.Count, entryIds.Count);
        }

        public (int tableGuids, int entryIds) FillExistingLocalizationReferences()
        {
            List<(int row, int column, string value)> tableGuids = new();
            List<(int row, int column, string value)> entryIds = new();
            CollectExistingLocalizationReferences(tableGuids, entryIds);

            foreach ((int row, int column, string value) field in tableGuids) SetCellValue(field.row, field.column, field.value);
            foreach ((int row, int column, string value) field in entryIds) SetCellValue(field.row, field.column, field.value);

            if (tableGuids.Count > 0 || entryIds.Count > 0) SaveRows();

            return (tableGuids.Count, entryIds.Count);
        }

        private void CollectExistingLocalizationReferences(List<(int row, int column, string value)> tableGuids, List<(int row, int column, string value)> entryIds)
        {
            if (!parsed || Column("SpeakerTableGuid") < 0 || Column("TextTableGuid") < 0 || Column("SpeakerEntryId") < 0 || Column("TextEntryId") < 0) return;

            IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
            int owner = -1;

            for (int row = 1; row < rows.Count; row++)
            {
                string type = GetCell(row, 1).Trim();

                if (DialogueCsvImporter.IsNodeType(type)) owner = row;

                if (type == "Dialogue" || type == "Choice")
                {
                    CollectExistingTableGuid(collections, row, row, true, tableGuids);
                    CollectExistingTableGuid(collections, row, row, false, tableGuids);
                    CollectExistingEntryId(collections, row, row, true, entryIds);

                    if (type == "Dialogue") CollectExistingEntryId(collections, row, row, false, entryIds);
                }
                else if (type == "Option" && owner > 0 && GetCell(owner, 1) == "Choice")
                {
                    CollectExistingTableGuid(collections, owner, row, false, tableGuids);
                    CollectExistingEntryId(collections, owner, row, false, entryIds);
                }
            }
        }

        private void CollectExistingTableGuid(IReadOnlyList<StringTableCollection> collections, int owner, int row, bool speaker, List<(int row, int column, string value)> changes)
        {
            int column = Column(speaker ? "SpeakerTableGuid" : "TextTableGuid");

            if (!string.IsNullOrWhiteSpace(GetCell(row, column)) || string.IsNullOrWhiteSpace(GetCell(owner, speaker ? 5 : 6))) return;

            StringTableCollection collection = FindExistingLocalizationTable(collections, owner, speaker);

            if (collection == null) return;

            changes.Add((row, column, collection.TableCollectionNameReference.TableCollectionNameGuid.ToString("N")));
        }

        private void CollectExistingEntryId(IReadOnlyList<StringTableCollection> collections, int owner, int row, bool speaker, List<(int row, int column, string value)> changes)
        {
            int column = Column(speaker ? "SpeakerEntryId" : "TextEntryId");

            if (!string.IsNullOrWhiteSpace(GetCell(row, column))) return;

            string key = GetCell(row, speaker ? 7 : 8).Trim();

            if (string.IsNullOrEmpty(key)) return;

            StringTableCollection collection = FindExistingLocalizationTable(collections, owner, speaker);

            if (collection == null) return;

            if (owner != row)
            {
                string optionGuid = GetCell(row, Column("TextTableGuid")).Trim();

                if (!string.IsNullOrEmpty(optionGuid))
                {
                    if (!Guid.TryParse(optionGuid, out Guid parsedOptionGuid) || parsedOptionGuid != collection.TableCollectionNameReference.TableCollectionNameGuid) return;
                }
            }

            SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(key);

            if (entry != null) changes.Add((row, column, entry.Id.ToString(CultureInfo.InvariantCulture)));
        }

        private StringTableCollection FindExistingLocalizationTable(IReadOnlyList<StringTableCollection> collections, int owner, bool speaker)
        {
            string guidText = GetCell(owner, Column(speaker ? "SpeakerTableGuid" : "TextTableGuid")).Trim();

            if (!string.IsNullOrEmpty(guidText))
            {
                if (!Guid.TryParse(guidText, out Guid guid)) return null;

                return collections.ResolveCollection("", guid) as StringTableCollection;
            }

            string name = GetCell(owner, speaker ? 5 : 6).Trim();

            if (string.IsNullOrEmpty(name) && options.useDefaultTables) name = DefaultTableName(speaker);

            foreach (StringTableCollection collection in collections)
            {
                if (collection.TableCollectionName == name) return collection;
            }

            return null;
        }

        public int GetColumnIndex(string name)
        {
            return Column(name);
        }

        public int GetLine(int row)
        {
            return row >= 0 && row < lines.Count ? lines[row] : 0;
        }

        public string GetColumnName(int column)
        {
            if (rows.Count == 0 || column < 0 || column >= rows[0].Count) return "";

            return rows[0][column];
        }

        public void SetCell(DialogueCsvIssue issue, string value)
        {
            if (!parsed || issue.row < 0 || issue.column < 0) return;

            if (IsOneClickConflict(issue.title) && !string.IsNullOrEmpty(issue.referenceScope))
            {
                foreach ((int row, int column) in GetConflictCells(issue)) SetCellValue(row, column, value);

                SaveRows();
                return;
            }

            if (IsSharedReference(issue))
            {
                string field = GetColumnName(issue.column);
                string suffix = field.StartsWith("Speaker", StringComparison.Ordinal) ? field.Substring("Speaker".Length) : field.Substring("Text".Length);
                bool onOwner = suffix == "Table" || suffix == "TableGuid";

                foreach (LocalizationReference reference in localizationReferences)
                {
                    if (GetReferenceScope(reference, issue.title) != issue.referenceScope) continue;

                    int row = onOwner ? reference.owner : reference.row;
                    int column = Column((reference.speaker ? "Speaker" : "Text") + suffix);

                    if (column < 0) continue;

                    SetCellValue(row, column, value);

                    if (!reference.speaker && suffix == "TableGuid" && GetCell(row, 1) == "Choice") SyncOptionGuids(row, value);
                }

                SaveRows();
                return;
            }

            List<int> affectedRows = GetAffectedRows(issue);
            string fieldName = GetColumnName(issue.column);

            foreach (int row in affectedRows)
            {
                SetCellValue(row, issue.column, value);

                if (fieldName == "TextTableGuid" && GetCell(row, 1) == "Choice") SyncOptionGuids(row, value);
            }

            SaveRows();
        }

        private void SyncOptionGuids(int owner, string guid)
        {
            int column = Column("TextTableGuid");

            if (column < 0) return;

            for (int row = owner + 1; row < rows.Count && !DialogueCsvImporter.IsNodeType(GetCell(row, 1)); row++)
            {
                if (GetCell(row, 1) == "Option") SetCellValue(row, column, guid);
            }
        }

        public void SyncChoiceTableGuid(int optionRow, bool useOption)
        {
            if (!parsed || optionRow < 2) return;

            int owner = optionRow - 1;

            while (owner > 0 && !DialogueCsvImporter.IsNodeType(GetCell(owner, 1))) owner--;

            if (GetCell(owner, 1) != "Choice") return;

            int guidColumn = Column("TextTableGuid");

            if (guidColumn < 0) return;

            string guid = GetCell(useOption ? optionRow : owner, guidColumn).Trim();
            SetCellValue(owner, guidColumn, guid);
            SyncOptionGuids(owner, guid);
            SaveRows();
        }

        public void InsertOption(int ownerRow)
        {
            if (!parsed || ownerRow < 1 || GetCell(ownerRow, 1) != "Choice") return;

            int insert = ownerRow + 1;

            while (insert < rows.Count && !DialogueCsvImporter.IsNodeType(GetCell(insert, 1))) insert++;

            List<string> option = new();

            for (int column = 0; column < rows[0].Count; column++) option.Add("");

            option[1] = "Option";
            int guidColumn = Column("TextTableGuid");

            if (guidColumn >= 0) option[guidColumn] = GetCell(ownerRow, guidColumn);

            rows.Insert(insert, option);
            SaveRows();
        }

        public void ApproveNewNode(DialogueCsvIssue issue)
        {
            if (issue.row < 1) return;

            options.approvedNewNodeIds.Add(GetCell(issue.row, 0).Trim());
            Scan();
        }

        public void ReplaceNodeId(DialogueCsvIssue issue, string existingId)
        {
            if (!parsed || issue.row < 1 || string.IsNullOrWhiteSpace(existingId)) return;

            string oldId = GetCell(issue.row, 0).Trim();

            for (int row = 1; row < rows.Count; row++)
            {
                if (GetCell(row, 2).Trim() == oldId) SetCellValue(row, 2, existingId);
            }

            SetCellValue(issue.row, 0, existingId);
            options.approvedNewNodeIds.Remove(oldId);
            SaveRows();
        }

        public void SetTableReference(DialogueCsvIssue issue, bool speaker, StringTableCollection collection)
        {
            if (!parsed || issue.row < 1 || collection == null) return;

            string guid = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString("N");

            if (IsSharedReference(issue))
            {
                foreach (LocalizationReference reference in localizationReferences)
                {
                    if (GetReferenceScope(reference, issue.title) != issue.referenceScope) continue;

                    string kind = reference.speaker ? "Speaker" : "Text";
                    SetCellValue(reference.owner, Column(kind + "Table"), collection.TableCollectionName);
                    int guidColumn = Column(kind + "TableGuid");

                    if (guidColumn >= 0) SetCellValue(reference.owner, guidColumn, guid);

                    if (!reference.speaker && GetCell(reference.owner, 1) == "Choice") SyncOptionGuids(reference.owner, guid);
                }
            }
            else
            {
                string kind = speaker ? "Speaker" : "Text";
                int nameColumn = Column(kind + "Table");
                int guidColumn = Column(kind + "TableGuid");

                foreach (int row in GetAffectedRows(issue))
                {
                    int owner = GetOwnerRow(row);
                    SetCellValue(owner, nameColumn, collection.TableCollectionName);

                    if (guidColumn >= 0) SetCellValue(owner, guidColumn, guid);

                    if (!speaker && GetCell(owner, 1) == "Choice") SyncOptionGuids(owner, guid);
                }
            }

            SaveRows();
        }

        public void CreateTableForReference(DialogueCsvIssue issue, bool speaker)
        {
            if (!parsed || issue.row < 1) return;

            if (IsSharedReference(issue))
            {
                string preferredName = "";

                if (issue.title == "Missing table GUID")
                {
                    string originKind = GetColumnName(issue.column).StartsWith("Speaker", StringComparison.Ordinal) ? "Speaker" : "Text";
                    preferredName = GetCell(issue.row, Column(originKind + "Table")).Trim();
                }

                foreach (LocalizationReference reference in localizationReferences)
                {
                    if (GetReferenceScope(reference, issue.title) != issue.referenceScope) continue;

                    int column = Column(reference.speaker ? "SpeakerTableGuid" : "TextTableGuid");

                    if (column >= 0) SetCellValue(reference.owner, column, "");
                    if (!string.IsNullOrEmpty(preferredName)) SetCellValue(reference.owner, Column(reference.speaker ? "SpeakerTable" : "TextTable"), preferredName);

                    if (!reference.speaker && GetCell(reference.owner, 1) == "Choice") SyncOptionGuids(reference.owner, "");
                }
            }
            else
            {
                int column = Column(speaker ? "SpeakerTableGuid" : "TextTableGuid");

                foreach (int row in GetAffectedRows(issue))
                {
                    int owner = GetOwnerRow(row);

                    if (column >= 0) SetCellValue(owner, column, "");

                    if (!speaker && GetCell(owner, 1) == "Choice") SyncOptionGuids(owner, "");
                }
            }

            options.createTables = true;
            SaveRows();
        }

        public void SetEntryReference(DialogueCsvIssue issue, bool speaker, string key, long id)
        {
            if (!parsed || issue.row < 0) return;

            if (IsSharedReference(issue))
            {
                foreach (LocalizationReference reference in localizationReferences)
                {
                    if (GetReferenceScope(reference, issue.title) != issue.referenceScope) continue;

                    SetCellValue(reference.row, reference.speaker ? 7 : 8, key);
                    int idColumn = Column(reference.speaker ? "SpeakerEntryId" : "TextEntryId");

                    if (idColumn >= 0) SetCellValue(reference.row, idColumn, id.ToString(CultureInfo.InvariantCulture));
                }
            }
            else
            {
                int keyColumn = speaker ? 7 : 8;
                int idColumn = Column(speaker ? "SpeakerEntryId" : "TextEntryId");

                foreach (int row in GetAffectedRows(issue))
                {
                    SetCellValue(row, keyColumn, key);

                    if (idColumn >= 0) SetCellValue(row, idColumn, id.ToString(CultureInfo.InvariantCulture));
                }
            }

            SaveRows();
        }

        private int GetOwnerRow(int row)
        {
            if (GetCell(row, 1) != "Option") return row;

            for (int i = row - 1; i > 0; i--)
            {
                if (DialogueCsvImporter.IsNodeType(GetCell(i, 1))) return i;
            }

            return row;
        }

        private static bool IsSharedReference(DialogueCsvIssue issue)
        {
            return issue.referenceScope != null && issue.referenceScope.StartsWith("shared-", StringComparison.Ordinal);
        }

        private static bool IsOneClickConflict(string title)
        {
            return title == "Conflicting entry keys" || title == "Conflicting entry IDs" || title == "Conflicting translation" || title == "Conflicting table names";
        }

        private List<(int row, int column)> GetConflictCells(DialogueCsvIssue issue)
        {
            string key = issue.referenceGroup ?? "";

            if (conflictCellsCache.TryGetValue(key, out List<(int row, int column)> cached)) return cached;

            List<(int row, int column)> cells = new();
            HashSet<(int row, int column)> seen = new();
            string field = GetColumnName(issue.column);
            int start = field.IndexOf('[');
            string locale = start >= 0 && field.EndsWith("]", StringComparison.Ordinal) ? field.Substring(start + 1, field.Length - start - 2) : "";

            foreach (LocalizationReference reference in localizationReferences)
            {
                if (GetReferenceScope(reference, issue.title) != issue.referenceScope) continue;

                string kind = reference.speaker ? "Speaker" : "Text";
                int row = issue.title == "Conflicting table names" ? reference.owner : reference.row;
                string targetField = issue.title switch
                {
                    "Conflicting entry keys" => kind + "Key",
                    "Conflicting entry IDs" => kind + "EntryId",
                    "Conflicting table names" => kind + "Table",
                    _ => kind + "[" + locale + "]",
                };
                int column = Column(targetField);

                if (column >= 0 && seen.Add((row, column))) cells.Add((row, column));
            }

            conflictCellsCache.Add(key, cells);

            return cells;
        }

        public List<DialogueCsvConflictChoice> GetConflictChoices(DialogueCsvIssue issue)
        {
            Dictionary<string, DialogueCsvConflictChoice> grouped = new(StringComparer.Ordinal);

            foreach ((int row, int column) in GetConflictCells(issue))
            {
                string value = GetCell(row, column);

                if (!grouped.TryGetValue(value, out DialogueCsvConflictChoice choice))
                {
                    choice = new DialogueCsvConflictChoice { value = value, firstLine = GetLine(row) };
                    grouped.Add(value, choice);
                }

                choice.count++;
            }

            List<DialogueCsvConflictChoice> result = new(grouped.Values);
            result.Sort((first, second) =>
            {
                int count = second.count.CompareTo(first.count);

                return count != 0 ? count : first.firstLine.CompareTo(second.firstLine);
            });

            return result;
        }

        public bool TryGetCurrentConflictValue(DialogueCsvIssue issue, out string value)
        {
            value = null;
            string field = GetColumnName(issue.column);
            bool speaker = field.StartsWith("Speaker", StringComparison.Ordinal);
            string kind = speaker ? "Speaker" : "Text";
            int owner = issue.row;

            if (!speaker && GetCell(owner, 1) == "Option")
            {
                while (owner > 1 && !DialogueCsvImporter.IsNodeType(GetCell(owner, 1))) owner--;
            }

            if (!Guid.TryParse(GetCell(owner, Column(kind + "TableGuid")), out Guid tableGuid)) return false;

            StringTableCollection collection = null;

            foreach (StringTableCollection table in LocalizationEditorSettings.GetStringTableCollections())
            {
                if (table.TableCollectionNameReference.TableCollectionNameGuid != tableGuid) continue;

                collection = table;
                break;
            }

            if (collection == null) return false;

            if (issue.title == "Conflicting table names")
            {
                value = collection.TableCollectionName;
                return true;
            }

            if (issue.title == "Conflicting entry IDs")
            {
                SharedTableData.SharedTableEntry byKey = collection.SharedData.GetEntry(GetCell(issue.row, speaker ? 7 : 8));

                if (byKey == null) return false;

                value = byKey.Id.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (!long.TryParse(GetCell(issue.row, Column(kind + "EntryId")), NumberStyles.Integer, CultureInfo.InvariantCulture, out long entryId)) return false;

            SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(entryId);

            if (entry == null) return false;

            if (issue.title == "Conflicting entry keys")
            {
                value = entry.Key;
                return true;
            }

            if (issue.title != "Conflicting translation") return false;

            int start = field.IndexOf('[');

            if (start < 0 || !field.EndsWith("]", StringComparison.Ordinal)) return false;

            string locale = field.Substring(start + 1, field.Length - start - 2);

            foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
            {
                if (reference.asset is not StringTable table || table.LocaleIdentifier.Code != locale) continue;

                StringTableEntry translated = table.GetEntry(entryId);

                if (translated == null) return false;

                value = translated.Value ?? "";
                return true;
            }

            return false;
        }

        public string GetAffectedFieldName(DialogueCsvIssue issue, int row)
        {
            if (IsSharedReference(issue))
            {
                HashSet<string> fields = new(StringComparer.Ordinal);
                string field = GetColumnName(issue.column);
                string suffix = field.StartsWith("Speaker", StringComparison.Ordinal) ? field.Substring("Speaker".Length) : field.Substring("Text".Length);
                bool onOwner = suffix == "Table" || suffix == "TableGuid";

                foreach (LocalizationReference reference in localizationReferences)
                {
                    if (GetReferenceScope(reference, issue.title) == issue.referenceScope && (onOwner ? reference.owner : reference.row) == row)
                        fields.Add((reference.speaker ? "Speaker" : "Text") + suffix);
                }

                if (fields.Count > 0) return string.Join(", ", fields);
            }

            if (IsOneClickConflict(issue.title) && !string.IsNullOrEmpty(issue.referenceScope))
            {
                List<string> fields = new();

                foreach ((int affectedRow, int column) in GetConflictCells(issue))
                {
                    if (affectedRow == row) fields.Add(GetColumnName(column));
                }

                if (fields.Count > 0) return string.Join(", ", fields);
            }

            return GetColumnName(issue.column);
        }

        public List<int> GetAffectedRows(DialogueCsvIssue issue)
        {
            List<int> result = new();

            if (issue.title == "Missing connection target" && !string.IsNullOrEmpty(issue.referenceScope))
            {
                string missingTarget = GetCell(issue.row, 2).Trim();

                for (int row = 1; row < rows.Count; row++)
                {
                    if (DialogueCsvImporter.AllowsNext(GetCell(row, 1)) && GetCell(row, 2).Trim() == missingTarget) result.Add(row);
                }

                return result;
            }

            if (IsOneClickConflict(issue.title) && !string.IsNullOrEmpty(issue.referenceScope))
            {
                HashSet<int> seen = new();

                foreach ((int row, int column) in GetConflictCells(issue))
                {
                    if (seen.Add(row)) result.Add(row);
                }

                return result;
            }

            if (string.IsNullOrEmpty(issue.referenceScope))
            {
                if (issue.row >= 0) result.Add(issue.row);

                return result;
            }

            string field = GetColumnName(issue.column);
            string cacheKey = issue.referenceGroup + "\u001e" + field;

            if (affectedRowsCache.TryGetValue(cacheKey, out List<int> cached)) return cached;
            else
            {
                HashSet<int> seen = new();
                bool onOwner = field == "SpeakerTable" || field == "TextTable" || field == "SpeakerTableGuid" || field == "TextTableGuid";

                foreach (LocalizationReference reference in localizationReferences)
                {
                    if (GetReferenceScope(reference, issue.title) != issue.referenceScope) continue;

                    int row = onOwner ? reference.owner : reference.row;

                    if (seen.Add(row)) result.Add(row);
                }

                affectedRowsCache.Add(cacheKey, result);

                return result;
            }
        }

        public string GetReferenceTable(DialogueCsvIssue issue)
        {
            foreach (LocalizationReference reference in localizationReferences)
            {
                if (reference.row == issue.row || reference.owner == issue.row)
                {
                    string field = GetColumnName(issue.column);
                    bool speaker = field.StartsWith("Speaker", StringComparison.Ordinal) || issue.column == 5;

                    if (reference.speaker == speaker) return reference.table;
                }
            }

            return "";
        }

        private void SetCellValue(int row, int column, string value)
        {
            while (rows[row].Count <= column) rows[row].Add("");

            rows[row][column] = value;
        }

        private void SaveRows()
        {
            List<IReadOnlyList<string>> output = new();

            foreach (List<string> cells in rows) output.Add(cells);

            csv = CsvUtility.ToCsv(output);
            Scan();
        }

        public void Scan()
        {
            issues.Clear();
            errorCount = 0;
            warningCount = 0;
            rows.Clear();
            lines.Clear();
            tables.Clear();
            columns.Clear();
            ids.Clear();
            instructionFields.Clear();
            localizationReferences.Clear();
            affectedRowsCache.Clear();
            conflictCellsCache.Clear();
            nodeLabels.Clear();
            tableNames.Clear();
            entryIds.Clear();
            entryKeys.Clear();
            translations.Clear();
            newTableNames.Clear();
            newEntryNames.Clear();
            renameNames.Clear();
            tableRenameNames.Clear();
            requestedTableNames.Clear();
            changedTranslations.Clear();
            nodeCount = 0;
            detailCount = 0;
            linkCount = 0;
            parsed = false;

            try
            {
                List<List<string>> parsedRows = CsvUtility.FromCsv(csv, out List<int> parsedLines);
                rows.AddRange(parsedRows);
                lines.AddRange(parsedLines);
                parsed = true;
            }
            catch (FormatException exception)
            {
                Add(-1, -1, "CSV parsing failed", exception.Message);
                FinalizeScan();
                return;
            }

            if (rows.Count == 0)
            {
                Add(-1, -1, "Empty CSV", "Select a CSV file with a header and dialogue rows.");
                FinalizeScan();
                return;
            }

            ScanHeaders();

            if (errorCount > 0)
            {
                FinalizeScan();
                return;
            }

            ScanRows();
            ScanLocalization();
            FinalizeScan();

            if (errorCount > 0) return;

            if (!DialogueCsvImporter.Validate(asset, csv, options, out string message))
            {
                int row = FindRowFromMessage(message);
                int column = FindColumnFromMessage(message);
                Add(row, column, "Import validation", message);
                FinalizeScan();
            }
        }

        private void ScanHeaders()
        {
            string[] required = { "ID", "Type", "Next", "Message", "Data", "SpeakerTable", "TextTable", "SpeakerKey", "TextKey", "X", "Y" };
            List<string> header = rows[0];
            HashSet<string> seen = new(StringComparer.Ordinal);

            for (int i = 0; i < required.Length; i++)
            {
                if (i < header.Count && header[i] == required[i]) continue;

                Add(0, i, "Required header", $"Column {i + 1} must be named '{required[i]}'.");
            }

            string[] identities = { "SpeakerTableGuid", "TextTableGuid", "SpeakerEntryId", "TextEntryId" };

            foreach (string identity in identities)
            {
                if (!header.Contains(identity)) Add(0, -1, "Missing identity column", $"The CSV must include '{identity}'. Export the current CSV format.");
            }

            for (int i = 0; i < header.Count; i++)
            {
                string name = header[i];

                if (!seen.Add(name)) Add(0, i, "Duplicate header", $"Column '{name}' appears more than once.");

                if (i >= required.Length && !IsSupportedColumn(name)) Add(0, i, "Unknown header", $"'{name}' is not a supported CSV column.");

                if (!columns.ContainsKey(name)) columns.Add(name, i);
            }
        }

        private void ScanRows()
        {
            Dictionary<string, int> seenIds = new(StringComparer.Ordinal);
            Dictionary<string, NodeType> existingTypes = new(StringComparer.Ordinal);
            existingTypes[asset.EditorStartNode.guid] = NodeType.Start;

            foreach (NodeData existing in asset.EditorNodes) existingTypes[existing.guid] = existing.type;

            bool parentTypeChanged = false;
            int existingOptionCount = 0;
            int currentOptionCount = 0;
            HashSet<int> choicesWithOption = new();
            List<int> choiceRows = new();
            List<int> details = new();
            int parent = -1;
            string expected = "";
            int startCount = 0;

            for (int r = 1; r < rows.Count; r++)
            {
                if (EmptyRow(r)) continue;

                if (rows[r].Count > rows[0].Count)
                    Add(r, rows[0].Count, "Extra columns", "This row contains more columns than the CSV header.");

                string type = GetCell(r, 1).Trim();
                string id = GetCell(r, 0).Trim();

                if (DialogueCsvImporter.IsNodeType(type))
                {
                    parent = r;
                    expected = GetExpectedChild(type);
                    nodeCount++;

                    parentTypeChanged = !existingTypes.TryGetValue(id, out NodeType previousType) || previousType.ToString() != type;
                    existingOptionCount = 0;
                    currentOptionCount = 0;

                    if (type == "Choice")
                    {
                        choiceRows.Add(r);

                        foreach (NodeData existing in asset.EditorNodes)
                        {
                            if (existing.guid != id || existing.type != NodeType.Choice) continue;

                            existingOptionCount = existing.EditorPortDatas.Count;
                            break;
                        }
                    }

                    if (type == "Start")
                    {
                        startCount++;

                        if (startCount > 1) Add(r, 1, "Duplicate Start node", "Only one Start node is allowed. Change this type or remove this row in the CSV.");
                    }

                    if (string.IsNullOrWhiteSpace(id)) Add(r, 0, "Invalid node ID", "Node ID must not be empty. Generate a unique ID or enter a name.");
                    else if (seenIds.TryGetValue(id, out int previous)) Add(r, 0, "Duplicate node ID", $"This ID is already used on line {lines[previous]}.");
                    else
                    {
                        seenIds.Add(id, r);
                        ids.Add(id, r);
                        nodeLabels.Add($"Line {lines[r]} · {type} · {id}");

                        if (existingTypes.Count > 1 && !existingTypes.ContainsKey(id) && !options.createNodes && !options.approvedNewNodeIds.Contains(id))
                            Add(r, 0, "New node ID", $"'{id}' is not in the current graph. Create a new node with this ID or reconnect it to an existing node.");
                    }
                }
                else if (DialogueCsvImporter.IsDetailType(type))
                {
                    detailCount++;
                    details.Add(r);

                    if (type == "Option" && parent >= 0 && GetCell(parent, 1) == "Choice")
                    {
                        choicesWithOption.Add(parent);
                        string parentGuid = GetCell(parent, Column("TextTableGuid")).Trim();
                        string optionGuid = GetCell(r, Column("TextTableGuid")).Trim();

                        if (!DialogueCsvImporter.SameGuid(parentGuid, optionGuid))
                        {
                            Add(r, Column("TextTableGuid"), "Option table GUID mismatch", "This Option must reference the same Text Table GUID as its Choice node. Choose which GUID to use.");
                            issues[issues.Count - 1].referenceGroup = "choice-guid:" + parent;
                        }
                    }

                    if (parent < 0 || type != expected)
                        Add(r, 1, "Invalid detail row", $"'{type}' requires a matching parent node. Expected: {(string.IsNullOrEmpty(expected) ? "node row" : expected)}.");
                }
                else
                {
                    Add(r, 1, "Unknown node type", $"'{type}' is not a supported node or detail type.");
                    parent = -1;
                    expected = "";
                    parentTypeChanged = false;
                }

                if (!string.IsNullOrEmpty(GetCell(r, 2))) linkCount++;

                CheckTypePayload(r, type);
                CheckNumber(r, "X", false, true);
                CheckNumber(r, "Y", false, true);
                CheckNumber(r, "SpeakerEntryId", true, false);
                CheckNumber(r, "TextEntryId", true, false);
                CheckGuid(r, "SpeakerTableGuid");
                CheckGuid(r, "TextTableGuid");
                bool addedOption = type == "Option" && currentOptionCount >= existingOptionCount;
                CheckData(r, type, parentTypeChanged || addedOption);

                if (type == "Option" && parent >= 0 && GetCell(parent, 1) == "Choice") currentOptionCount++;

                if (type == "Instruction" && parent >= 0 && (GetCell(parent, 1) == "Command" || GetCell(parent, 1) == "Event"))
                    CheckUnmatchedArguments(r, GetCell(parent, 1) == "Event");
            }

            foreach (int choiceRow in choiceRows)
            {
                if (!choicesWithOption.Contains(choiceRow)) Add(choiceRow, 1, "Missing Option", "Choice requires at least one Option row. Add an Option row below this Choice.");
            }

            if (startCount == 0)
            {
                if (options.keepStart && string.IsNullOrEmpty(asset.EditorStartNode.guid))
                    Add(-1, -1, "Start node unavailable", "The existing Start node has no ID. Choose Create Start.");
                else Add(-1, -1, "Start node", options.keepStart ? "The existing Start node will be preserved." : "A new Start node will be generated.", true);
            }

            if (options.keepStart && startCount == 0 && seenIds.ContainsKey(asset.EditorStartNode.guid))
                Add(-1, -1, "Start ID conflict", "A CSV node uses the existing Start ID. Choose Create Start or change that node's ID.");

            for (int r = 1; r < rows.Count; r++)
            {
                if (EmptyRow(r)) continue;

                string next = GetCell(r, 2).Trim();

                if (DialogueCsvImporter.AllowsNext(GetCell(r, 1)) && !string.IsNullOrEmpty(next) && !ids.ContainsKey(next) && !(startCount == 0 && options.keepStart && next == asset.EditorStartNode.guid))
                {
                    Add(r, 2, "Missing connection target", $"The Next ID '{next}' does not exist in the imported graph. Choose a node or clear this link.");
                    DialogueCsvIssue issue = issues[issues.Count - 1];
                    issue.referenceScope = "missing-next:" + next;
                    issue.referenceGroup = "missing-next:" + next;
                    issue.referenceLabel = "Missing Next target · " + next;
                }
            }
        }

        private void ScanLocalization()
        {
            IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();

            foreach (StringTableCollection collection in collections)
            {
                tables[collection.TableCollectionName] = collection;
                tableNames.Add(collection.TableCollectionName);
            }

            tableNames.Sort(StringComparer.Ordinal);
            HashSet<string> localeCodes = new();
            IReadOnlyList<Locale> locales = LocalizationEditorSettings.GetLocales();

            foreach (Locale locale in locales) localeCodes.Add(locale.Identifier.Code);

            foreach (KeyValuePair<string, int> header in columns)
            {
                if (!TryLocaleCode(header.Key, out string code) || localeCodes.Contains(code)) continue;

                Add(0, header.Value, "Unknown locale", $"Locale '{code}' is not configured in Localization Settings. Its values would not be imported.");
            }

            if (options.createTables && !AssetDatabase.IsValidFolder(options.tableFolder))
                Add(-1, -1, "Invalid table folder", $"'{options.tableFolder}' is not a valid folder under Assets.");

            int parent = -1;
            int optionIndex = 0;

            for (int r = 1; r < rows.Count; r++)
            {
                if (EmptyRow(r)) continue;

                string type = GetCell(r, 1).Trim();

                if (DialogueCsvImporter.IsNodeType(type))
                {
                    parent = r;
                    optionIndex = 0;

                    if (type == "Dialogue")
                    {
                        ScanEntry(r, r, true, "speaker", localeCodes);
                        ScanEntry(r, r, false, "text", localeCodes);
                    }
                    else if (type == "Choice") ScanEntry(r, r, true, "speaker", localeCodes);
                }
                else if (type == "Option" && parent >= 0 && GetCell(parent, 1) == "Choice")
                {
                    ScanEntry(parent, r, false, $"opt{optionIndex}", localeCodes);
                    optionIndex++;
                }
            }

            CheckFinalTableAssignments();
            CheckFinalKeyAssignments();
            AnnotateLocalizationIssues();
            newTableCount = newTableNames.Count;
            newEntryCount = newEntryNames.Count;
            renameCount = renameNames.Count;
            tableRenameCount = tableRenameNames.Count;
            translationCount = changedTranslations.Count;
        }

        private void ScanEntry(int owner, int row, bool speaker, string suffix, HashSet<string> localeCodes)
        {
            string kind = speaker ? "Speaker" : "Text";
            string name = GetCell(owner, speaker ? 5 : 6).Trim();
            string key = GetCell(row, speaker ? 7 : 8).Trim();
            string idText = GetCell(row, Column(kind + "EntryId")).Trim();
            long.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id);
            int tableColumn = speaker ? 5 : 6;

            string guidText = GetCell(owner, Column(kind + "TableGuid")).Trim();
            bool hasEntry = id > 0 || !string.IsNullOrEmpty(key);
            List<(string code, string value, int column)> values = new();

            foreach (KeyValuePair<string, int> column in columns)
            {
                if (!column.Key.StartsWith(kind + "[", StringComparison.Ordinal) || !column.Key.EndsWith("]", StringComparison.Ordinal)) continue;

                string code = column.Key.Substring(kind.Length + 1, column.Key.Length - kind.Length - 2);
                string value = GetCell(row, column.Value);

                if (localeCodes.Contains(code) && (hasEntry || !string.IsNullOrEmpty(value)))
                    values.Add((code, value, column.Value));
            }

            if (values.Count == 0 && id == 0 && string.IsNullOrEmpty(key)) return;

            StringTableCollection collection = null;

            if (Guid.TryParse(guidText, out Guid guid))
            {
                IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
                collection = collections.ResolveCollection("", guid) as StringTableCollection;

                if (collection != null)
                {
                    if (string.IsNullOrEmpty(name)) name = collection.TableCollectionName;

                    if (requestedTableNames.TryGetValue(guid, out (string name, int row) previous) && previous.name != name)
                        Add(owner, tableColumn, "Conflicting table names", $"Line {lines[previous.row]} gives this same table GUID a different name '{previous.name}'. Choose one name.");
                    else requestedTableNames[guid] = (name, owner);

                    if (name != collection.TableCollectionName) tableRenameNames.Add($"{collection.TableCollectionName} → {name} ({guid:N})");
                    tables[name] = collection;
                }
            }
            else if (string.IsNullOrEmpty(guidText))
            {
                if (string.IsNullOrEmpty(name) && options.useDefaultTables) name = DefaultTableName(speaker);

                tables.TryGetValue(name, out collection);
            }

            if (string.IsNullOrEmpty(key) && (id > 0 || values.Count > 0))
                Add(row, speaker ? 7 : 8, "Missing key name", $"{kind} localization needs a CSV key. Enter a key or select an existing entry.");

            localizationReferences.Add(new LocalizationReference { row = row, owner = owner, speaker = speaker, table = name, guid = guidText, key = key, id = id });

            if (!string.IsNullOrEmpty(guidText) && collection == null)
            {
                if (Guid.TryParse(guidText, out _))
                    Add(owner, Column(kind + "TableGuid"), "Missing table GUID", $"{kind}TableGuid '{guidText}' does not exist in Localization. The table name cannot replace a supplied GUID.");

                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                Add(owner, tableColumn, "Missing table name", $"{kind} data is present but no table is selected. Enter a table name or enable the asset's default table.");
                return;
            }

            if (collection == null)
            {
                if (!options.createTables) Add(owner, tableColumn, "Missing Localization table", $"String Table '{name}' does not exist. Select an existing table or enable Create missing tables.");
                else newTableNames.Add(name);
            }
            else if (string.IsNullOrEmpty(guidText) && Column(kind + "TableGuid") >= 0 && !(options.useDefaultTables && string.IsNullOrEmpty(GetCell(owner, tableColumn))))
                Add(owner, Column(kind + "TableGuid"), "Missing table GUID reference", $"'{name}' exists. Select this table to store its GUID in the CSV instead of linking by name.");

            SharedTableData.SharedTableEntry byId = id > 0 ? collection?.SharedData.GetEntry(id) : null;
            SharedTableData.SharedTableEntry byKey = string.IsNullOrEmpty(key) ? null : collection?.SharedData.GetEntry(key);
            SharedTableData.SharedTableEntry existing = null;

            if (id > 0)
            {
                if (byId == null)
                {
                    if (!options.createEntries)
                        Add(row, Column(kind + "EntryId"), "Missing Localization entry ID", $"Entry ID {id} does not exist in '{name}'. Choose Create missing entries or select an existing entry.");
                    else if (!string.IsNullOrEmpty(key)) newEntryNames.Add($"{name}:{id}:{key}");

                    if (string.IsNullOrEmpty(key)) Add(row, speaker ? 7 : 8, "Missing key name", $"Entry ID {id} requires a non-empty key in the CSV.");
                }
                else
                {
                    existing = byId;

                    if (string.IsNullOrEmpty(key))
                        Add(row, speaker ? 7 : 8, "Missing key name", $"Enter the CSV key name for entry ID {id}.");
                    else if (byId.Key != key) renameNames.Add($"{name}:{byId.Id}:{key}");
                }
            }
            else
            {
                if (byKey != null)
                {
                    existing = byKey;
                    id = byKey.Id;
                    localizationReferences[localizationReferences.Count - 1].id = id;
                }

                if (byKey == null && (collection != null || options.createTables))
                {
                    if (!options.createEntries) Add(row, speaker ? 7 : 8, "Missing Localization entry", $"Entry '{key}' does not exist in '{name}'. Choose Create missing entries or select an existing entry.");
                    else newEntryNames.Add($"{name}:0:{key}");
                }
            }

            if (existing != null) id = existing.Id;

            if (id > 0)
            {
                (string table, long id) identity = (name, id);

                if (entryIds.TryGetValue(identity, out (string key, int row) previous) && previous.key != key)
                    Add(row, speaker ? 7 : 8, "Conflicting entry keys", $"Line {lines[previous.row]} assigns a different key '{previous.key}' to ID {id}.");
                else entryIds[identity] = (key, row);
            }

            if (!string.IsNullOrEmpty(key))
            {
                (string table, string key) identity = (name, key);

                if (entryKeys.TryGetValue(identity, out (long id, int row) previous) && previous.id != id && previous.id != 0 && id != 0)
                    Add(row, Column(kind + "EntryId"), "Conflicting entry IDs", $"Line {lines[previous.row]} assigns another ID {previous.id} to key '{key}'.");
                else entryKeys[identity] = (id, row);
            }

            string entryIdentity = id != 0 ? $"id:{id}" : $"key:{key}";

            foreach ((string code, string value, int column) in values)
            {
                (string table, string entry, string locale) identity = (name, entryIdentity, code);

                if (translations.TryGetValue(identity, out (string text, int row) previous) && previous.text != value)
                {
                    Add(row, column, "Conflicting translation", $"Line {lines[previous.row]} uses '{Preview(previous.text)}' for the same entry and locale {code}. Choose one value.");
                    DialogueCsvIssue issue = issues[issues.Count - 1];
                    issue.alternateRow = previous.row;
                    issue.alternateValue = previous.text;
                }
                else translations[identity] = (value, row);

                if (collection == null)
                {
                    if (!string.IsNullOrEmpty(value)) changedTranslations.Add($"{name}:{entryIdentity}:{code}");

                    continue;
                }

                StringTable localeTable = null;

                foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
                {
                    if (reference.asset is not StringTable table || table.LocaleIdentifier.Code != code) continue;

                    localeTable = table;
                    break;
                }

                StringTableEntry entry = existing == null ? null : localeTable?.GetEntry(existing.Id);

                if (entry == null && string.IsNullOrEmpty(value)) continue;

                if (localeTable == null)
                {
                    if (!options.createLocaleTables)
                        Add(row, column, "Locale table missing", $"'{name}' has no String Table for '{code}'. Choose Create missing locale tables.");
                    else changedTranslations.Add($"{name}:{entryIdentity}:{code}");

                    continue;
                }

                if (entry == null || (entry.Value ?? "") != value) changedTranslations.Add($"{name}:{entryIdentity}:{code}");
            }
        }

        private void CheckFinalTableAssignments()
        {
            Dictionary<string, Guid> nameOwners = new(StringComparer.Ordinal);

            foreach (KeyValuePair<Guid, (string name, int row)> assignment in requestedTableNames)
            {
                if (nameOwners.TryGetValue(assignment.Value.name, out Guid previousGuid) && previousGuid != assignment.Key)
                {
                    int guidColumn = Column("SpeakerTableGuid");

                    if (!Guid.TryParse(GetCell(assignment.Value.row, guidColumn), out Guid speakerGuid) || speakerGuid != assignment.Key)
                        guidColumn = Column("TextTableGuid");

                    Add(assignment.Value.row, guidColumn, "Table name conflict", $"Table name '{assignment.Value.name}' is assigned to multiple table GUIDs.");
                }
                else nameOwners[assignment.Value.name] = assignment.Key;
            }

            foreach (KeyValuePair<Guid, (string name, int row)> assignment in requestedTableNames)
            {
                StringTableCollection occupied = null;

                foreach (StringTableCollection collection in LocalizationEditorSettings.GetStringTableCollections())
                {
                    if (collection.TableCollectionName != assignment.Value.name) continue;

                    occupied = collection;
                    break;
                }

                if (occupied == null || occupied.TableCollectionNameReference.TableCollectionNameGuid == assignment.Key) continue;

                Guid occupiedGuid = occupied.TableCollectionNameReference.TableCollectionNameGuid;

                if (requestedTableNames.TryGetValue(occupiedGuid, out (string name, int row) replacement) && replacement.name != occupied.TableCollectionName) continue;

                int nameColumn = 5;
                int speakerGuidColumn = Column("SpeakerTableGuid");

                if (!Guid.TryParse(GetCell(assignment.Value.row, speakerGuidColumn), out Guid sourceGuid) || sourceGuid != assignment.Key) nameColumn = 6;

                Add(assignment.Value.row, nameColumn, "Table name conflict", $"'{assignment.Value.name}' belongs to a different table GUID ({occupiedGuid}). Change the CSV name or connect to that table.");
            }
        }

        private void CheckFinalKeyAssignments()
        {
            foreach (KeyValuePair<(string table, long id), (string key, int row)> assignment in entryIds)
            {
                if (!tables.TryGetValue(assignment.Key.table, out StringTableCollection collection)) continue;

                SharedTableData.SharedTableEntry occupied = collection.SharedData.GetEntry(assignment.Value.key);

                if (occupied == null || occupied.Id == assignment.Key.id) continue;

                if (entryIds.TryGetValue((assignment.Key.table, occupied.Id), out (string key, int row) replacement) && replacement.key != occupied.Key) continue;

                int column = 8;

                foreach (LocalizationReference reference in localizationReferences)
                {
                    if (reference.row != assignment.Value.row || reference.table != assignment.Key.table || reference.id != assignment.Key.id) continue;

                    column = reference.speaker ? 7 : 8;
                    break;
                }

                Add(assignment.Value.row, column, "Key name conflict", $"Key '{assignment.Value.key}' already belongs to entry ID {occupied.Id}. Rename or reconnect that entry.");
            }
        }

        private void AnnotateLocalizationIssues()
        {
            foreach (DialogueCsvIssue issue in issues)
            {
                bool tableIssue = issue.title == "Missing table name" || issue.title == "Missing Localization table" || issue.title == "Missing table GUID" || issue.title == "Invalid table GUID" || issue.title == "Missing table GUID reference" || issue.title == "Conflicting table names" || issue.title == "Table name conflict";
                bool entryIssue = issue.title == "Missing Localization entry" || issue.title == "Missing Localization entry ID" || issue.title == "Missing key name" ||
                    issue.title == "Entry ID mismatch" || issue.title == "Key name conflict" ||
                    issue.title == "Conflicting entry keys" || issue.title == "Conflicting entry IDs" ||
                    issue.title == "Conflicting translation" || issue.title == "Locale table missing" ||
                    issue.title == "Translation would be ignored" || issue.title == "Invalid entry ID";

                if ((!tableIssue && !entryIssue) || issue.row < 1 || issue.column < 0) continue;

                string field = GetColumnName(issue.column);
                bool speaker = field.StartsWith("Speaker", StringComparison.Ordinal) || issue.column == 5;
                bool text = field.StartsWith("Text", StringComparison.Ordinal) || issue.column == 6;

                if (!speaker && !text) continue;

                bool onOwner = field == "SpeakerTable" || field == "TextTable" || field == "SpeakerTableGuid" || field == "TextTableGuid";

                foreach (LocalizationReference reference in localizationReferences)
                {
                    if (reference.speaker != speaker || (onOwner ? reference.owner : reference.row) != issue.row) continue;

                    string scope = GetReferenceScope(reference, issue.title);
                    TryLocaleCode(field, out string locale);
                    issue.referenceScope = scope;
                    issue.referenceGroup = issue.title + "\u001e" + scope + "\u001e" + locale;
                    string kind = speaker ? "Speaker" : "Text";
                    string identity = !string.IsNullOrEmpty(reference.key) ? reference.key : $"ID {reference.id}";

                    if (reference.id > 0 && !string.IsNullOrEmpty(reference.key)) identity += $" (ID {reference.id})";

                    issue.referenceLabel = tableIssue ? $"{kind} table · {(string.IsNullOrEmpty(reference.table) ? "Unassigned" : reference.table)}" : $"{kind} · {reference.table} · {identity}";
                    break;
                }
            }
        }

        private static string GetReferenceScope(LocalizationReference reference, string title)
        {
            string kind = reference.speaker ? "Speaker" : "Text";
            bool hasGuid = Guid.TryParse(reference.guid, out Guid parsedGuid);
            string tableGuid = hasGuid ? parsedGuid.ToString("N") : "";

            if (title == "Missing table GUID" && hasGuid) return "shared-table-guid:" + tableGuid;
            if ((title == "Missing Localization table" || title == "Missing table GUID reference") && !string.IsNullOrEmpty(reference.table) && !hasGuid)
                return "shared-table-name:" + reference.table;

            bool entryIssue = title == "Missing Localization entry ID" || title == "Missing Localization entry" || title == "Missing key name" || title == "Key name conflict" || title == "Entry ID mismatch" || title == "Invalid entry ID";

            if (entryIssue && hasGuid)
            {
                if (reference.id > 0) return "shared-entry:" + tableGuid + "\u001f" + reference.id.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(reference.key)) return "shared-entry-key:" + tableGuid + "\u001f" + reference.key;
            }

            if (title == "Missing table name") return kind + "\u001fowner:" + reference.owner;
            if (title == "Missing Localization table") return kind + "\u001ftable:" + reference.table;
            if (title == "Missing table GUID" || title == "Invalid table GUID") return kind + "\u001fguid:" + reference.guid;
            if (title == "Locale table missing") return "locale-table:" + reference.table;
            if (title == "Missing table GUID reference") return kind + "\u001ftable:" + reference.table;
            string tableIdentity = Guid.TryParse(reference.guid, out Guid guid) ? "guid:" + guid.ToString("N") : "table:" + reference.table;

            if (title == "Conflicting table names") return "table:" + tableIdentity;
            if (title == "Table name conflict") return kind + "\u001fguid:" + reference.guid;
            if (title == "Conflicting entry IDs") return "entry-key:" + tableIdentity + "\u001f" + reference.key;

            if (title == "Conflicting entry keys" || title == "Conflicting translation")
            {
                string entryIdentity = reference.id > 0 ? "id:" + reference.id.ToString(CultureInfo.InvariantCulture) : "key:" + reference.key;

                return "entry:" + tableIdentity + "\u001f" + entryIdentity;
            }

            string identity = reference.id > 0 ? "id:" + reference.id.ToString(CultureInfo.InvariantCulture) :
                string.IsNullOrEmpty(reference.key) ? "row:" + reference.row : "key:" + reference.key;

            return kind + "\u001f" + reference.table + "\u001f" + identity;
        }

        private void CheckNumber(int row, string field, bool positiveInteger, bool finiteFloat)
        {
            int column = Column(field);
            string value = GetCell(row, column).Trim();

            if (column < 0 || string.IsNullOrEmpty(value)) return;

            if (positiveInteger && (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long entryId) || entryId <= 0))
                Add(row, column, "Invalid entry ID", $"{field} must be a positive integer or empty.");

            if (finiteFloat && (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float position) || float.IsNaN(position) || float.IsInfinity(position)))
                Add(row, column, "Invalid position", $"{field} must be a finite number or empty for automatic layout.");
        }

        private void CheckGuid(int row, string field)
        {
            int column = Column(field);
            string value = GetCell(row, column).Trim();

            if (column >= 0 && !string.IsNullOrEmpty(value) && !Guid.TryParse(value, out _))
                Add(row, column, "Invalid table GUID", $"{field} must be a GUID or empty to use the table name.");
        }

        private void CheckTypePayload(int row, string type)
        {
            if (!DialogueCsvImporter.IsNodeType(type) && !DialogueCsvImporter.IsDetailType(type)) return;

            int column = -1;

            if (DialogueCsvImporter.IsDetailType(type) && !string.IsNullOrWhiteSpace(GetCell(row, 0))) column = 0;
            else if (DialogueCsvImporter.IsDetailType(type) && !string.IsNullOrWhiteSpace(GetCell(row, 9))) column = 9;
            else if (DialogueCsvImporter.IsDetailType(type) && !string.IsNullOrWhiteSpace(GetCell(row, 10))) column = 10;
            else if (!DialogueCsvImporter.AllowsNext(type) && !string.IsNullOrWhiteSpace(GetCell(row, 2))) column = 2;
            else if (!DialogueCsvImporter.AllowsMessage(type) && !string.IsNullOrEmpty(GetCell(row, 3))) column = 3;
            else if (!DialogueCsvImporter.AllowsData(type) && !string.IsNullOrWhiteSpace(GetCell(row, 4))) column = 4;
            else if (!DialogueCsvImporter.AllowsSpeaker(type)) column = FindLocalizationField(row, "Speaker", true);

            if (column < 0 && !DialogueCsvImporter.AllowsTextTable(type)) column = FindLocalizationField(row, "Text", true);
            else if (column < 0 && !DialogueCsvImporter.AllowsTextEntry(type)) column = FindLocalizationField(row, "Text", false);

            if (column < 0) return;

            Add(row, column, "Unused data for Type", $"{type} does not use '{GetColumnName(column)}'. Clear this field or choose a compatible Type before import.");
            issues[issues.Count - 1].referenceGroup = "type-data:" + row;
        }

        private int FindLocalizationField(int row, string kind, bool includeTable)
        {
            string[] fixedNames = includeTable ? new[] { kind + "Table", kind + "TableGuid", kind + "Key", kind + "EntryId" } :
                new[] { kind + "Key", kind + "EntryId" };

            foreach (string name in fixedNames)
            {
                int column = Column(name);

                if (column >= 0 && !string.IsNullOrWhiteSpace(GetCell(row, column))) return column;
            }

            foreach (KeyValuePair<string, int> column in columns)
            {
                if (column.Key.StartsWith(kind + "[", StringComparison.Ordinal) && !string.IsNullOrEmpty(GetCell(row, column.Value))) return column.Value;
            }

            return -1;
        }

        private bool HasLocalizedValue(int row, bool speaker)
        {
            string kind = speaker ? "Speaker" : "Text";
            int keyColumn = speaker ? 7 : 8;

            if (!string.IsNullOrWhiteSpace(GetCell(row, keyColumn)) || !string.IsNullOrWhiteSpace(GetCell(row, Column(kind + "EntryId")))) return true;

            foreach (KeyValuePair<string, int> column in columns)
            {
                if (column.Key.StartsWith(kind + "[", StringComparison.Ordinal) && !string.IsNullOrEmpty(GetCell(row, column.Value))) return true;
            }

            return false;
        }

        private void CheckData(int row, string type, bool typeChanged)
        {
            string data = GetCell(row, 4).Trim();
            bool valid = type switch
            {
                "Option" => DialogueCsvImporter.IsValidConditions(data, true),
                "Branch" => DialogueCsvImporter.IsValidConditions(data, false),
                "Trigger" => DialogueCsvImporter.IsValidTrigger(data),
                "Instruction" => DialogueCsvImporter.IsValidArguments(data),
                "Wait" => DialogueCsvImporter.IsValidWait(data),
                _ => true,
            };

            if (!valid) Add(row, 4, "Invalid Data", $"{type} requires a valid expression or value.");

            if (typeChanged && type == "Trigger" && string.IsNullOrWhiteSpace(data))
                Add(row, 4, "Missing required Data", "Trigger requires at least one setter.");

            if (typeChanged && type == "Dialogue" && !HasLocalizedValue(row, false))
                Add(row, 8, "Missing required localization", "Dialogue requires a Text localization entry.");

            if (typeChanged && type == "Option" && !HasLocalizedValue(row, false))
                Add(row, 8, "Missing required localization", "Option requires a Text localization entry.");

            if (typeChanged && type == "Instruction" && string.IsNullOrWhiteSpace(GetCell(row, 3)))
                Add(row, 3, "Missing instruction ID", "Instruction requires a command or event ID.");
        }

        private void CheckUnmatchedArguments(int row, bool isEvent)
        {
            string data = GetCell(row, 4);

            if (!DialogueCsvImporter.IsValidArguments(data)) return;

            (bool isEvent, string schemaId) key = (isEvent, GetCell(row, 3));

            if (!instructionFields.TryGetValue(key, out HashSet<int> fields))
            {
                CommandNode.TryGetSchemaFieldIds(key.schemaId, isEvent, out fields);
                instructionFields.Add(key, fields);
            }

            if (fields == null) return;

            foreach (string raw in data.Split(';'))
            {
                string part = raw.Trim();
                int separator = part.IndexOf(':');

                if (separator <= 0) continue;

                string idText = Uri.UnescapeDataString(part.Substring(0, separator).Trim());

                if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || fields.Contains(id)) continue;

                Add(row, 4, "Unmatched instruction ID", $"Field ID {id} is not in the current schema. Its value will be preserved. After import, use Resolve Unmatched IDs on the node.", true);
            }
        }

        private string DefaultTableName(bool speaker)
        {
            LocalizedStringTable table = speaker ? asset.SpeakerTable : asset.TextTable;

            if (table == null || table.IsEmpty) return "";

            TableReference reference = table.TableReference;
            Guid guid = reference.TableCollectionNameGuid;

            if (guid != Guid.Empty)
            {
                IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
                StringTableCollection collection = collections.ResolveCollection("", guid) as StringTableCollection;

                return collection?.TableCollectionName ?? "";
            }

            return reference.TableCollectionName ?? "";
        }

        private void FinalizeScan()
        {
            errorCount = 0;
            warningCount = 0;

            foreach (DialogueCsvIssue issue in issues)
            {
                if (issue.warning) warningCount++;
                else errorCount++;
            }

            newTableCount = newTableNames.Count;
            newEntryCount = newEntryNames.Count;
            renameCount = renameNames.Count;
            tableRenameCount = tableRenameNames.Count;
            translationCount = changedTranslations.Count;
        }

        private void Add(int row, int column, string title, string detail, bool warning = false)
        {
            int line = row >= 0 && row < lines.Count ? lines[row] : 0;
            issues.Add(new DialogueCsvIssue { row = row, column = column, line = line, title = title, detail = detail, warning = warning });

            if (!warning) errorCount++;
        }

        private int Column(string name) => columns.TryGetValue(name, out int index) ? index : -1;

        private bool EmptyRow(int row)
        {
            foreach (string value in rows[row])
            {
                if (!string.IsNullOrWhiteSpace(value)) return false;
            }

            return true;
        }

        private static string GetExpectedChild(string parent) => parent switch
        {
            "Choice" => "Option",
            "Condition" => "Branch",
            "Command" => "Instruction",
            "Event" => "Instruction",
            _ => "",
        };

        private static bool IsSupportedColumn(string name)
        {
            if (name == "SpeakerTableGuid" || name == "TextTableGuid" || name == "SpeakerEntryId" || name == "TextEntryId") return true;

            return TryLocaleCode(name, out _);
        }

        private static bool TryLocaleCode(string name, out string code)
        {
            string[] prefixes = { "Speaker[", "Text[" };

            foreach (string prefix in prefixes)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith("]", StringComparison.Ordinal)) continue;

                code = name.Substring(prefix.Length, name.Length - prefix.Length - 1);

                return !string.IsNullOrWhiteSpace(code);
            }

            code = "";
            return false;
        }

        private int FindRowFromMessage(string message)
        {
            int index = message.IndexOf("row ", StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                index = message.IndexOf("rows ", StringComparison.OrdinalIgnoreCase);

                if (index < 0) return -1;

                index += 5;
                int andIndex = message.IndexOf(" and ", index, StringComparison.OrdinalIgnoreCase);

                if (andIndex >= 0) index = andIndex + 5;
            }
            else index += 4;

            int end = index;

            while (end < message.Length && char.IsDigit(message[end])) end++;

            if (!int.TryParse(message.Substring(index, end - index), out int line)) return -1;

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == line) return i;
            }

            return -1;
        }

        private int FindColumnFromMessage(string message)
        {
            foreach (KeyValuePair<string, int> column in columns)
            {
                if (message.IndexOf(column.Key, StringComparison.Ordinal) >= 0) return column.Value;
            }

            return -1;
        }

        private sealed class LocalizationReference
        {
            public int row;
            public int owner;
            public bool speaker;
            public string table;
            public string guid;
            public string key;
            public long id;
        }

        private static string Preview(string text)
        {
            text = text.Replace('\n', ' ').Replace('\r', ' ');

            return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
        }
    }
}
