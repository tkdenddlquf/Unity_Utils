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
        private readonly HashSet<string> changedTranslations = new();
        private readonly Dictionary<string, StringTableCollection> tables = new();
        private readonly Dictionary<string, int> columns = new();
        private readonly Dictionary<string, int> ids = new();
        private readonly List<int> lines = new();

        public readonly DialogueCsvImportOptions options = new();
        public readonly List<DialogueCsvIssue> issues = new();
        public readonly List<List<string>> rows = new();
        public readonly List<string> nodeLabels = new();
        public readonly List<string> tableNames = new();
        public IReadOnlyCollection<string> plannedTables => newTableNames;
        public IReadOnlyCollection<string> plannedEntries => newEntryNames;
        public IReadOnlyCollection<string> plannedRenames => renameNames;
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

        public string GetColumnName(int column)
        {
            if (rows.Count == 0 || column < 0 || column >= rows[0].Count) return "";

            return rows[0][column];
        }

        public void SetCell(int row, int column, string value)
        {
            if (!parsed || row < 0 || row >= rows.Count || column < 0) return;

            while (rows[row].Count <= column) rows[row].Add("");

            rows[row][column] = value;
            SaveRows();
        }

        public void SetEntryReference(int row, bool speaker, string key, long id)
        {
            int keyColumn = speaker ? 7 : 8;
            int idColumn = Column(speaker ? "SpeakerEntryId" : "TextEntryId");

            if (row < 0 || row >= rows.Count || idColumn < 0) return;

            rows[row][keyColumn] = key;

            while (rows[row].Count <= idColumn) rows[row].Add("");

            rows[row][idColumn] = id.ToString(CultureInfo.InvariantCulture);
            SaveRows();
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
            nodeLabels.Clear();
            tableNames.Clear();
            entryIds.Clear();
            entryKeys.Clear();
            translations.Clear();
            newTableNames.Clear();
            newEntryNames.Clear();
            renameNames.Clear();
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
            Dictionary<string, int> seenGuids = new(StringComparer.OrdinalIgnoreCase);
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

                    if (type == "Start")
                    {
                        startCount++;

                        if (startCount > 1) Add(r, 1, "Duplicate Start node", "Only one Start node is allowed. Change this type or remove this row in the CSV.");
                    }

                    if (!Guid.TryParse(id, out _)) Add(r, 0, "Invalid node ID", "Node ID must be a GUID. Use Generate ID to assign a new one.");
                    else if (seenGuids.TryGetValue(id, out int previous)) Add(r, 0, "Duplicate node ID", $"This ID is already used on line {lines[previous]}.");
                    else
                    {
                        seenGuids.Add(id, r);
                        ids.Add(id, r);
                        nodeLabels.Add($"Line {lines[r]} · {type} · {id}");
                    }
                }
                else if (DialogueCsvImporter.IsDetailType(type))
                {
                    detailCount++;
                    details.Add(r);

                    if (parent < 0 || type != expected)
                        Add(r, 1, "Invalid detail row", $"'{type}' requires a matching parent node. Expected: {(string.IsNullOrEmpty(expected) ? "node row" : expected)}.");
                }
                else
                {
                    Add(r, 1, "Unknown node type", $"'{type}' is not a supported node or detail type.");
                    parent = -1;
                    expected = "";
                }

                if (!string.IsNullOrEmpty(GetCell(r, 2))) linkCount++;

                CheckNumber(r, "X", false, true);
                CheckNumber(r, "Y", false, true);
                CheckNumber(r, "SpeakerEntryId", true, false);
                CheckNumber(r, "TextEntryId", true, false);
                CheckGuid(r, "SpeakerTableGuid");
                CheckGuid(r, "TextTableGuid");
                CheckPresence(r);
                CheckData(r, type);
            }

            if (startCount == 0)
            {
                if (options.keepStart && string.IsNullOrEmpty(asset.EditorStartNode.guid))
                    Add(-1, -1, "Start node unavailable", "The existing Start node has no ID. Choose Create Start.");
                else Add(-1, -1, "Start node", options.keepStart ? "The existing Start node will be preserved." : "A new Start node will be generated.", true);
            }

            if (options.keepStart && startCount == 0 && seenGuids.ContainsKey(asset.EditorStartNode.guid))
                Add(-1, -1, "Start ID conflict", "A CSV node uses the existing Start ID. Choose Create Start or change that node's ID.");

            for (int r = 1; r < rows.Count; r++)
            {
                if (EmptyRow(r)) continue;

                string next = GetCell(r, 2).Trim();

                if (!string.IsNullOrEmpty(next) && !ids.ContainsKey(next) && !(startCount == 0 && options.keepStart && next == asset.EditorStartNode.guid))
                    Add(r, 2, "Missing connection target", $"The Next ID '{next}' does not exist in the imported graph. Choose a node or clear this link.");
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

            newTableCount = newTableNames.Count;
            newEntryCount = newEntryNames.Count;
            renameCount = renameNames.Count;
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

            List<(string code, string value, int column)> values = new();

            foreach (KeyValuePair<string, int> column in columns)
            {
                if (!column.Key.StartsWith(kind + "[", StringComparison.Ordinal) || !column.Key.EndsWith("]", StringComparison.Ordinal)) continue;

                string code = column.Key.Substring(kind.Length + 1, column.Key.Length - kind.Length - 2);
                string present = GetCell(row, Column(kind + "Present[" + code + "]")).Trim();
                string value = GetCell(row, column.Value);

                if (!localeCodes.Contains(code)) continue;

                if (present == "1" || string.IsNullOrEmpty(present) && !string.IsNullOrEmpty(value)) values.Add((code, value, column.Value));
            }

            if (values.Count == 0 && id == 0 && string.IsNullOrEmpty(key)) return;

            string guidText = GetCell(owner, Column(kind + "TableGuid")).Trim();

            if (Guid.TryParse(guidText, out Guid guid))
            {
                IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
                StringTableCollection byGuid = collections.ResolveCollection("", guid) as StringTableCollection;

                if (byGuid != null) name = byGuid.TableCollectionName;
            }

            if (string.IsNullOrEmpty(name) && options.useDefaultTables) name = DefaultTableName(speaker);

            if (string.IsNullOrEmpty(name))
            {
                Add(owner, tableColumn, "Missing table name", $"{kind} data is present but no table is selected. Enter a table name or enable the asset's default table.");
                return;
            }

            tables.TryGetValue(name, out StringTableCollection collection);

            if (collection == null)
            {
                if (!options.createTables) Add(owner, tableColumn, "Missing Localization table", $"String Table '{name}' does not exist. Select an existing table or enable Create missing tables.");
                else newTableNames.Add(name);
            }

            if (string.IsNullOrEmpty(key) && id == 0)
            {
                string ownerId = GetCell(owner, 0).Trim();
                key = ownerId + "_" + suffix;
            }

            SharedTableData.SharedTableEntry byId = collection?.SharedData.GetEntry(id);
            SharedTableData.SharedTableEntry byKey = string.IsNullOrEmpty(key) ? null : collection?.SharedData.GetEntry(key);

            if (id > 0 && byId == null && byKey != null && byKey.Id != id)
                Add(row, Column(kind + "EntryId"), "Entry ID mismatch", $"'{key}' is registered as ID {byKey.Id}, not {id}. Choose the existing ID or key.");

            if (byId != null && byKey != null && byId.Id != byKey.Id)
                Add(row, speaker ? 7 : 8, "Key name conflict", $"The requested key '{key}' already belongs to ID {byKey.Id}.");

            if (byId != null && !string.IsNullOrEmpty(key) && byId.Key != key && byKey == null)
                renameNames.Add($"{name}:{byId.Id}:{key}");

            SharedTableData.SharedTableEntry existing = byId ?? byKey;

            if (existing != null)
            {
                id = existing.Id;

                if (string.IsNullOrEmpty(key)) key = existing.Key;
            }
            else if (collection != null || options.createTables)
            {
                if (!options.createEntries) Add(row, speaker ? 7 : 8, "Missing Localization entry", $"Entry '{key}' does not exist in '{name}'. Enable Create missing entries to add it.");
                else newEntryNames.Add($"{name}:{id}:{key}");
            }

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
                    changedTranslations.Add($"{name}:{entryIdentity}:{code}");
                    continue;
                }

                StringTable localeTable = null;

                foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
                {
                    if (reference.asset is not StringTable table || table.LocaleIdentifier.Code != code) continue;

                    localeTable = table;
                    break;
                }

                if (localeTable == null)
                {
                    Add(row, column, "Locale table missing", $"'{name}' has no String Table for '{code}'. Add this locale table in Unity Localization before importing.");
                    continue;
                }

                StringTableEntry entry = existing == null ? null : localeTable.GetEntry(existing.Id);

                if (entry == null || entry.Value != value) changedTranslations.Add($"{name}:{entryIdentity}:{code}");
            }
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

        private void CheckPresence(int row)
        {
            foreach (KeyValuePair<string, int> column in columns)
            {
                string name = column.Key;

                if (!name.StartsWith("TextPresent[", StringComparison.Ordinal) && !name.StartsWith("SpeakerPresent[", StringComparison.Ordinal)) continue;

                string value = GetCell(row, column.Value).Trim();

                if (value != "0" && value != "1")
                {
                    Add(row, column.Value, "Invalid presence", "Presence must be 0 (missing) or 1 (entry exists).");
                    continue;
                }

                string textColumn = name.Replace("TextPresent[", "Text[").Replace("SpeakerPresent[", "Speaker[");
                int textIndex = Column(textColumn);

                if (value == "0" && textIndex >= 0 && !string.IsNullOrEmpty(GetCell(row, textIndex)))
                    Add(row, column.Value, "Translation would be ignored", $"{textColumn} has a value but {name} is 0. Set presence to 1 or clear the translation.");
            }
        }

        private void CheckData(int row, string type)
        {
            string data = GetCell(row, 4).Trim();
            bool valid = type switch
            {
                "Option" => DialogueCsvImporter.IsValidConditions(data, true),
                "Branch" => DialogueCsvImporter.IsValidConditions(data, false),
                "Trigger" => DialogueCsvImporter.IsValidTrigger(data),
                "Instruction" => DialogueCsvImporter.IsValidArguments(data),
                _ => true,
            };

            if (!valid) Add(row, 4, "Invalid Data", $"{type} data has an invalid field, operator, type, or value.");
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
            string[] prefixes = { "Speaker[", "Text[", "SpeakerPresent[", "TextPresent[" };

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

            if (index < 0) return -1;

            int start = index + 4;
            int end = start;

            while (end < message.Length && char.IsDigit(message[end])) end++;

            if (!int.TryParse(message.Substring(start, end - start), out int line)) return -1;

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

        private static string Preview(string text)
        {
            text = text.Replace('\n', ' ').Replace('\r', ' ');

            return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
        }
    }
}
