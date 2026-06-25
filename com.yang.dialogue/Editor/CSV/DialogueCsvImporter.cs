using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Imports a CSV produced in the <see cref="DialogueCsvExporter"/> format back into a
    /// <see cref="DialogueSO"/>: rebuilds nodes/links and writes localized text into the
    /// String Table Collections referenced by the SO's Speaker/Text overrides.
    /// </summary>
    public static class DialogueCsvImporter
    {
        // Fixed column index layout (FIXED_COLUMNS = count; locale columns follow):
        //   0 : ID, 1 : Type, 2 : Next, 3 : Message, 4 : Data,
        //   5 : SpeakerTable, 6 : TextTable, 7 : SpeakerKey, 8 : TextKey, 9 : X, 10 : Y
        private const int COL_ID = 0;
        private const int COL_TYPE = 1;
        private const int COL_NEXT = 2;
        private const int COL_MESSAGE = 3;
        private const int COL_DATA = 4;
        private const int COL_SPEAKER_TABLE = 5;
        private const int COL_TEXT_TABLE = 6;
        private const int COL_SPEAKER_KEY = 7;
        private const int COL_TEXT_KEY = 8;
        private const int COL_X = 9;
        private const int COL_Y = 10;
        private const int FIXED_COLUMNS = 11;

        /// <summary>Maps a locale code to its Speaker and Text column indices in the CSV.</summary>
        private class LocaleColumn
        {
            public string code;
            public int speakerColumn;
            public int textColumn;
        }

        /// <summary>Parsed representation of one CSV row plus its localized values and any child sub-rows.</summary>
        private class Row
        {
            public string id;
            public string type;
            public string next;
            public string message;
            public string data;
            public string speakerTable;
            public string textTable;
            public string speakerKey;
            public string textKey;
            public string posX;
            public string posY;

            public readonly Dictionary<string, string> speaker = new();
            public readonly Dictionary<string, string> text = new();

            public readonly List<Row> children = new();
        }

        /// <summary>Shared import state: known collections, resolved tables, warnings, and existing object slots.</summary>
        private class Ctx
        {
            public IReadOnlyList<StringTableCollection> collections;
            public readonly Dictionary<string, StringTableCollection> resolvedTables = new();
            public readonly List<string> warnings = new();
            public readonly Dictionary<string, List<DataWrapper>> existingObjects = new();
        }

        /// <summary>Parses the CSV and rebuilds the SO's nodes, links and localized text; returns false on abort.</summary>
        public static bool Import(DialogueSO so, string csv, out string message)
        {
            message = "";

            List<List<string>> rows = CsvUtility.FromCsv(csv);

            if (rows.Count < 1)
            {
                message = "CSV is empty.";

                return false;
            }

            List<LocaleColumn> localeColumns = ParseHeader(rows[0]);

            List<Row> nodes = ParseRows(rows, localeColumns);

            ResolveIds(nodes);

            Ctx ctx = new()
            {
                collections = LocalizationEditorSettings.GetStringTableCollections(),
            };

            foreach (NodeData node in so.EditorNodes)
            {
                if (node.type == NodeType.Object) ctx.existingObjects[node.guid] = node.EditorOptionDatas;
            }

            if (!ResolveTables(so, nodes, ctx, out message)) return false;

            Build(so, nodes, ctx);

            if (ctx.warnings.Count > 0) message = "Imported with warnings:\n- " + string.Join("\n- ", ctx.warnings);

            return true;
        }

        /// <summary>Reads the header row into per-locale Speaker/Text column mappings.</summary>
        private static List<LocaleColumn> ParseHeader(List<string> header)
        {
            Dictionary<string, LocaleColumn> byCode = new();

            for (int i = FIXED_COLUMNS; i < header.Count; i++)
            {
                string name = header[i];

                if (TryParseLocaleHeader(name, "Speaker", out string code))
                {
                    LocaleColumn column = GetOrAdd(byCode, code);

                    column.speakerColumn = i;
                }
                else if (TryParseLocaleHeader(name, "Text", out code))
                {
                    LocaleColumn column = GetOrAdd(byCode, code);

                    column.textColumn = i;
                }
            }

            return new List<LocaleColumn>(byCode.Values);
        }

        /// <summary>Returns the locale column for a code, creating an empty one if absent.</summary>
        private static LocaleColumn GetOrAdd(Dictionary<string, LocaleColumn> map, string code)
        {
            if (!map.TryGetValue(code, out LocaleColumn column))
            {
                column = new LocaleColumn { code = code, speakerColumn = -1, textColumn = -1 };

                map.Add(code, column);
            }

            return column;
        }

        /// <summary>Extracts the locale code from a "Prefix[code]" header, returning false if it doesn't match.</summary>
        private static bool TryParseLocaleHeader(string name, string prefix, out string code)
        {
            code = "";

            if (!name.StartsWith(prefix + "[") || !name.EndsWith("]")) return false;

            int start = prefix.Length + 1;

            code = name.Substring(start, name.Length - start - 1);

            return true;
        }

        /// <summary>Parses data rows into node rows, attaching Option/Branch/Asset rows as children of the prior node.</summary>
        private static List<Row> ParseRows(List<List<string>> rows, List<LocaleColumn> localeColumns)
        {
            List<Row> nodes = new();

            Row current = null;

            for (int r = 1; r < rows.Count; r++)
            {
                List<string> cells = rows[r];

                if (IsEmptyRow(cells)) continue;

                Row row = ReadRow(cells, localeColumns);

                if (row.type == "Option" || row.type == "Branch" || row.type == "Asset")
                {
                    current?.children.Add(row);
                }
                else
                {
                    nodes.Add(row);

                    current = row;
                }
            }

            return nodes;
        }

        /// <summary>Ensures each node row has a unique id, prompting per node to generate or skip on missing/duplicate ids.</summary>
        private static void ResolveIds(List<Row> nodes)
        {
            HashSet<string> seen = new();

            bool generateAll = false;

            foreach (Row row in nodes)
            {
                if (string.IsNullOrEmpty(row.type)) continue;

                bool missing = string.IsNullOrEmpty(row.id);
                bool duplicate = !missing && seen.Contains(row.id);

                if (missing || duplicate)
                {
                    if (generateAll)
                    {
                        row.id = System.Guid.NewGuid().ToString();
                    }
                    else
                    {
                        string title = missing ? "Missing Node ID" : "Duplicate Node ID";
                        string body = missing
                            ? $"A '{row.type}' node has no ID.\n\nGenerate a new ID for it?"
                            : $"The ID '{row.id}' is already used by another node.\n\nGenerate a new ID for this '{row.type}' node?";

                        int choice = EditorUtility.DisplayDialogComplex(title, body, "Generate", "Skip Node", "Generate All");

                        if (choice == 2) generateAll = true;

                        row.id = choice == 1 ? "" : System.Guid.NewGuid().ToString();
                    }
                }

                if (!string.IsNullOrEmpty(row.id)) seen.Add(row.id);
            }
        }

        /// <summary>Returns true when every cell in the row is null or empty.</summary>
        private static bool IsEmptyRow(List<string> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (!string.IsNullOrEmpty(cells[i])) return false;
            }

            return true;
        }

        /// <summary>Builds a <see cref="Row"/> from raw cells, trimming structural fields and preserving content whitespace.</summary>
        private static Row ReadRow(List<string> cells, List<LocaleColumn> localeColumns)
        {
            Row row = new()
            {
                id = Cell(cells, COL_ID),
                type = Cell(cells, COL_TYPE),
                next = Cell(cells, COL_NEXT),
                message = Content(Cell(cells, COL_MESSAGE, false)),
                data = Cell(cells, COL_DATA),
                speakerTable = Cell(cells, COL_SPEAKER_TABLE),
                textTable = Cell(cells, COL_TEXT_TABLE),
                speakerKey = Cell(cells, COL_SPEAKER_KEY),
                textKey = Cell(cells, COL_TEXT_KEY),
                posX = Cell(cells, COL_X),
                posY = Cell(cells, COL_Y),
            };

            foreach (LocaleColumn column in localeColumns)
            {
                string speaker = Content(Cell(cells, column.speakerColumn, false));
                string text = Content(Cell(cells, column.textColumn, false));

                if (!string.IsNullOrEmpty(speaker)) row.speaker[column.code] = speaker;
                if (!string.IsNullOrEmpty(text)) row.text[column.code] = text;
            }

            return row;
        }

        /// <summary>Safely reads a cell by index, returning empty when out of range and trimming when requested.</summary>
        private static string Cell(List<string> cells, int index, bool trim = true)
        {
            if (index < 0 || index >= cells.Count) return "";

            string value = cells[index];

            return trim ? value.Trim() : value;
        }

        /// <summary>Treats a whitespace-only content value as empty while preserving surrounding whitespace otherwise.</summary>
        private static string Content(string value) => string.IsNullOrWhiteSpace(value) ? "" : value;

        /// <summary>Constructs nodes, links and positions from the parsed rows and writes them onto the SO.</summary>
        private static void Build(DialogueSO so, List<Row> rows, Ctx ctx)
        {
            Undo.RecordObject(so, "Import Dialogue CSV");

            List<NodeData> nodeList = new();
            List<LinkData> links = new();

            List<(string source, int port, string target)> linkRequests = new();

            NodeData startNode = default;
            bool hasStart = false;

            string firstNodeId = null;

            foreach (Row row in rows)
            {
                if (string.IsNullOrEmpty(row.id) || string.IsNullOrEmpty(row.type)) continue;

                if (row.type == "Start")
                {
                    startNode = new NodeData(NodeType.Start) { guid = row.id };

                    hasStart = true;

                    AddLink(linkRequests, row.id, 0, row.next);

                    continue;
                }

                NodeData node = BuildNode(row, ctx, linkRequests);

                nodeList.Add(node);

                firstNodeId ??= node.guid;
            }

            if (!hasStart)
            {
                startNode = new NodeData(NodeType.Start);

                if (firstNodeId != null) AddLink(linkRequests, startNode.guid, 0, firstNodeId);
            }

            HashSet<string> validIds = new() { startNode.guid };

            foreach (NodeData node in nodeList) validIds.Add(node.guid);

            List<string> droppedLinks = new();

            foreach ((string source, int port, string target) in linkRequests)
            {
                if (!validIds.Contains(target))
                {
                    droppedLinks.Add($"{source} → {target}");

                    continue;
                }

                links.Add(new LinkData { nodeGuid = source, targetGuid = target, outPortIndex = port });
            }

            if (droppedLinks.Count > 0)
                ctx.warnings.Add($"{droppedLinks.Count} link(s) dropped — target node missing or skipped: " + string.Join(", ", droppedLinks));

            Dictionary<string, Vector2> explicitPositions = new();

            foreach (Row row in rows)
            {
                if (string.IsNullOrEmpty(row.id)) continue;

                if (float.TryParse(row.posX, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(row.posY, NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                {
                    explicitPositions[row.id] = new Vector2(x, y);
                }
            }

            AssignPositions(ref startNode, nodeList, links, explicitPositions);

            so.EditorStartNode = startNode;

            so.EditorNodes.Clear();
            so.EditorNodes.AddRange(nodeList);

            so.EditorLinks.Clear();
            so.EditorLinks.AddRange(links);

            EditorUtility.SetDirty(so);

            AssetDatabase.SaveAssets();
        }

        /// <summary>Queues a link request from a source port to a target, ignoring empty targets.</summary>
        private static void AddLink(List<(string, int, string)> requests, string source, int port, string target)
        {
            if (!string.IsNullOrEmpty(target)) requests.Add((source, port, target));
        }

        /// <summary>Builds a single <see cref="NodeData"/> from a row, populating options, ports and link requests by type.</summary>
        private static NodeData BuildNode(Row row, Ctx ctx, List<(string, int, string)> linkRequests)
        {
            NodeType type = ParseType(row.type);

            NodeData node = new(type) { guid = row.id };

            List<DataWrapper> options = node.EditorOptionDatas;
            List<DataWrapper> ports = node.EditorPortDatas;

            StringTableCollection speakerCol = ResolveTable(ctx, row.speakerTable);
            StringTableCollection textCol = ResolveTable(ctx, row.textTable);

            switch (type)
            {
                case NodeType.Dialogue:
                    {
                        string speakerKey = ResolveKey(row.speakerKey, row.id, "speaker");
                        string textKey = ResolveKey(row.textKey, row.id, "text");

                        options.Add(MakeTableEntry(speakerCol, speakerKey, row.speaker, out _));
                        options.Add(MakeEntryRef(speakerCol, speakerKey, row.speaker));
                        options.Add(MakeTableEntry(textCol, textKey, row.text, out _));
                        options.Add(MakeEntryRef(textCol, textKey, row.text));
                        options.Add(new DataWrapper(new GenericData(row.message)));

                        ports.Add(new DataWrapper());

                        AddLink(linkRequests, row.id, 0, row.next);
                    }
                    break;

                case NodeType.Choice:
                    {
                        string speakerKey = ResolveKey(row.speakerKey, row.id, "speaker");

                        options.Add(MakeTableEntry(speakerCol, speakerKey, row.speaker, out _));
                        options.Add(MakeEntryRef(speakerCol, speakerKey, row.speaker));
                        options.Add(MakeTableName(textCol, HasContent(row, true)));
                        options.Add(new DataWrapper(new GenericData(row.message)));

                        for (int i = 0; i < row.children.Count; i++)
                        {
                            Row option = row.children[i];

                            string key = ResolveKey(option.textKey, row.id, $"opt{i}");

                            long id = WriteEntry(textCol, key, option.text);

                        List<GenericData> data = new()
                        {
                            id != 0 ? new GenericData(key) : new GenericData(GenericData.DataType.String),
                            id != 0 ? new GenericData(id) : new GenericData(GenericData.DataType.Long),
                            new GenericData(ParseHide(option.data)),
                        };

                        AppendConditions(data, option.data);

                        ports.Add(new DataWrapper(data));

                        AddLink(linkRequests, row.id, i, option.next);
                    }

                        if (ports.Count == 0)
                        {
                            ports.Add(new DataWrapper(
                                new GenericData(GenericData.DataType.String),
                                new GenericData(GenericData.DataType.Long),
                                new GenericData(GenericData.DataType.Bool)));
                        }
                    }
                    break;

                case NodeType.Trigger:
                    AppendTrigger(options, row.data);

                    ports.Add(new DataWrapper());

                    AddLink(linkRequests, row.id, 0, row.next);
                    break;

                case NodeType.Event:
                    AppendEvent(options, row.data);

                    ports.Add(new DataWrapper());

                    AddLink(linkRequests, row.id, 0, row.next);
                    break;

                case NodeType.Wait:
                    options.Add(MakeWait(row.data));

                    ports.Add(new DataWrapper());

                    AddLink(linkRequests, row.id, 0, row.next);
                    break;

                case NodeType.Condition:
                    ports.Add(new DataWrapper());

                    AddLink(linkRequests, row.id, 0, row.next);

                    for (int i = 0; i < row.children.Count; i++)
                    {
                        Row branch = row.children[i];

                        List<GenericData> data = new();

                        AppendConditions(data, branch.data);

                        ports.Add(new DataWrapper(data));

                        AddLink(linkRequests, row.id, i + 1, branch.next);
                    }
                    break;

                case NodeType.Object:
                    if (ctx.existingObjects.TryGetValue(row.id, out List<DataWrapper> existing) && existing.Count > 0)
                    {
                        foreach (DataWrapper slot in existing) options.Add(new DataWrapper(slot));
                    }
                    else
                    {
                        int slotCount = row.children.Count > 0 ? row.children.Count : 1;

                        for (int i = 0; i < slotCount; i++) options.Add(new DataWrapper(new GenericData(GenericData.DataType.Object)));

                        ctx.warnings.Add($"'{row.id}': new Object node imported with {slotCount} empty slot(s) — assign objects in the editor.");
                    }

                    ports.Add(new DataWrapper());

                    AddLink(linkRequests, row.id, 0, row.next);
                    break;
            }

            return node;
        }

        /// <summary>Returns true when the row (or optionally any child) has localized text.</summary>
        private static bool HasContent(Row row, bool checkChildren)
        {
            if (row.text.Count > 0) return true;

            if (checkChildren)
            {
                foreach (Row child in row.children)
                {
                    if (child.text.Count > 0) return true;
                }
            }

            return false;
        }

        /// <summary>Resolves (and optionally creates) every Speaker/Text table referenced by content rows; returns false to abort.</summary>
        private static bool ResolveTables(DialogueSO so, List<Row> nodes, Ctx ctx, out string error)
        {
            error = "";

            ApplyDefaultTables(so, nodes, ctx);

            List<(string name, string nodeId, string kind)> needed = new();

            foreach (Row row in nodes)
            {
                if (string.IsNullOrEmpty(row.id)) continue;

                NodeType type = ParseType(row.type);

                if (type == NodeType.Dialogue)
                {
                    if (row.speaker.Count > 0) needed.Add((row.speakerTable, row.id, "Speaker"));
                    if (row.text.Count > 0) needed.Add((row.textTable, row.id, "Text"));
                }
                else if (type == NodeType.Choice)
                {
                    if (row.speaker.Count > 0) needed.Add((row.speakerTable, row.id, "Speaker"));

                    foreach (Row option in row.children)
                    {
                        if (option.text.Count > 0)
                        {
                            needed.Add((row.textTable, row.id, "Text"));

                            break;
                        }
                    }
                }
            }

            foreach ((string name, string nodeId, string kind) in needed)
            {
                if (string.IsNullOrEmpty(name))
                {
                    error = $"Node '{nodeId}' has {kind} text but its {kind} table name is empty.\nImport aborted.";

                    return false;
                }
            }

            foreach ((string name, string nodeId, string kind) in needed)
            {
                if (ctx.resolvedTables.ContainsKey(name)) continue;

                StringTableCollection existing = FindCollection(ctx.collections, name);

                if (existing != null)
                {
                    ctx.resolvedTables[name] = existing;

                    continue;
                }

                bool create = EditorUtility.DisplayDialog(
                    "Table Not Found",
                    $"The {kind} table '{name}' (used by node '{nodeId}') does not exist.\n\nCreate it?",
                    "Create",
                    "Cancel");

                if (!create)
                {
                    error = $"Import aborted: {kind} table '{name}' does not exist.";

                    return false;
                }

                string absolute = EditorUtility.SaveFolderPanel($"Select a folder for new table '{name}'", "Assets", name);

                if (string.IsNullOrEmpty(absolute))
                {
                    error = $"Import aborted: no folder selected for new table '{name}'.";

                    return false;
                }

                string relative = ToProjectRelative(absolute);

                if (relative == null)
                {
                    error = "Import aborted: the selected folder must be inside this project's Assets folder.";

                    return false;
                }

                StringTableCollection created = LocalizationEditorSettings.CreateStringTableCollection(name, relative);

                if (created == null)
                {
                    error = $"Import aborted: failed to create table '{name}'.";

                    return false;
                }

                ctx.collections = LocalizationEditorSettings.GetStringTableCollections();
                ctx.resolvedTables[name] = created;
            }

            return true;
        }

        /// <summary>Fills empty Speaker/Text table names on content rows with the SO's default table, prompting once per default.</summary>
        private static void ApplyDefaultTables(DialogueSO so, List<Row> nodes, Ctx ctx)
        {
            string speakerDefault = GetDefaultTableName(so.SpeakerTable, ctx);
            string textDefault = GetDefaultTableName(so.TextTable, ctx);

            if (string.IsNullOrEmpty(speakerDefault) && string.IsNullOrEmpty(textDefault)) return;

            Dictionary<string, bool> decisions = new();

            foreach (Row row in nodes)
            {
                if (string.IsNullOrEmpty(row.id)) continue;

                NodeType type = ParseType(row.type);

                if (type == NodeType.Dialogue)
                {
                    row.speakerTable = ResolveDefault(row.speaker.Count > 0, row.speakerTable, speakerDefault, "Speaker", row.id, decisions);
                    row.textTable = ResolveDefault(row.text.Count > 0, row.textTable, textDefault, "Text", row.id, decisions);
                }
                else if (type == NodeType.Choice)
                {
                    row.speakerTable = ResolveDefault(row.speaker.Count > 0, row.speakerTable, speakerDefault, "Speaker", row.id, decisions);

                    bool hasOptionText = false;

                    foreach (Row option in row.children)
                    {
                        if (option.text.Count > 0)
                        {
                            hasOptionText = true;

                            break;
                        }
                    }

                    row.textTable = ResolveDefault(hasOptionText, row.textTable, textDefault, "Text", row.id, decisions);
                }
            }
        }

        /// <summary>Substitutes the SO default table name for an empty one when the (cached) user choice agrees.</summary>
        private static string ResolveDefault(bool hasText, string tableName, string defaultName, string kind, string nodeId, Dictionary<string, bool> decisions)
        {
            if (!hasText || !string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(defaultName)) return tableName;

            if (!decisions.TryGetValue(defaultName, out bool use))
            {
                use = EditorUtility.DisplayDialog(
                    $"Use Default {kind} Table?",
                    $"Node '{nodeId}' has {kind} text but no {kind} table name in the CSV.\n\nUse the dialogue's default {kind} table '{defaultName}'?",
                    "Use Default",
                    "Skip");

                decisions[defaultName] = use;
            }

            return use ? defaultName : tableName;
        }

        /// <summary>Resolves the SO's default <see cref="LocalizedStringTable"/> to a collection name (matching by GUID if needed).</summary>
        private static string GetDefaultTableName(LocalizedStringTable table, Ctx ctx)
        {
            if (table == null || table.IsEmpty) return "";

            TableReference reference = table.TableReference;

            if (!string.IsNullOrEmpty(reference.TableCollectionName)) return reference.TableCollectionName;

            System.Guid guid = reference.TableCollectionNameGuid;

            if (guid != System.Guid.Empty)
            {
                for (int i = 0; i < ctx.collections.Count; i++)
                {
                    if (ctx.collections[i].TableCollectionNameReference.TableCollectionNameGuid == guid)
                        return ctx.collections[i].TableCollectionName;
                }
            }

            return "";
        }

        /// <summary>Returns the already-resolved collection for a table name, or null.</summary>
        private static StringTableCollection ResolveTable(Ctx ctx, string name)
            => !string.IsNullOrEmpty(name) && ctx.resolvedTables.TryGetValue(name, out StringTableCollection collection) ? collection : null;

        /// <summary>Finds a collection by its display name within the given list, or null.</summary>
        private static StringTableCollection FindCollection(IReadOnlyList<StringTableCollection> collections, string name)
        {
            for (int i = 0; i < collections.Count; i++)
            {
                if (collections[i].TableCollectionName == name) return collections[i];
            }

            return null;
        }

        /// <summary>Converts an absolute path under the project's Assets folder to an "Assets/..." path, or null if outside.</summary>
        private static string ToProjectRelative(string absolute)
        {
            absolute = absolute.Replace('\\', '/');

            string dataPath = Application.dataPath.Replace('\\', '/');

            if (absolute == dataPath) return "Assets";
            if (absolute.StartsWith(dataPath + "/")) return "Assets" + absolute.Substring(dataPath.Length);

            return null;
        }

        /// <summary>Writes the entry's locale values and returns a table-name descriptor slot for it.</summary>
        private static DataWrapper MakeTableEntry(StringTableCollection collection, string key, Dictionary<string, string> values, out long id)
        {
            id = WriteEntry(collection, key, values);

            return MakeTableName(collection, id != 0);
        }

        /// <summary>Builds a name+guid table descriptor slot, or an empty slot when there is no content.</summary>
        private static DataWrapper MakeTableName(StringTableCollection collection, bool hasContent)
        {
            if (collection == null || !hasContent)
            {
                return new DataWrapper(
                    new GenericData(GenericData.DataType.String),
                    new GenericData(GenericData.DataType.Guid));
            }

            return new DataWrapper(
                new GenericData(collection.TableCollectionName),
                new GenericData(collection.TableCollectionNameReference.TableCollectionNameGuid));
        }

        /// <summary>Builds a key+id reference slot for an entry, or an empty slot when it has no id.</summary>
        private static DataWrapper MakeEntryRef(StringTableCollection collection, string key, Dictionary<string, string> values)
        {
            long id = collection == null ? 0 : LookupId(collection, key, values);

            if (id == 0)
            {
                return new DataWrapper(
                    new GenericData(GenericData.DataType.String),
                    new GenericData(GenericData.DataType.Long));
            }

            return new DataWrapper(new GenericData(key), new GenericData(id));
        }

        /// <summary>Returns the shared entry id for a key when values exist, or 0.</summary>
        private static long LookupId(StringTableCollection collection, string key, Dictionary<string, string> values)
        {
            if (values.Count == 0) return 0;

            SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(key);

            return entry == null ? 0 : entry.Id;
        }

        /// <summary>Creates/updates the key and writes per-locale values. Returns the key id (0 if nothing written).</summary>
        private static long WriteEntry(StringTableCollection collection, string key, Dictionary<string, string> values)
        {
            if (collection == null || values.Count == 0) return 0;

            SharedTableData shared = collection.SharedData;

            SharedTableData.SharedTableEntry entry = shared.GetEntry(key) ?? shared.AddKey(key);

            long id = entry.Id;

            foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
            {
                if (reference.asset is StringTable table)
                {
                    string code = table.LocaleIdentifier.Code;

                    if (values.TryGetValue(code, out string value) && !string.IsNullOrEmpty(value))
                    {
                        table.AddEntry(id, value);

                        EditorUtility.SetDirty(table);
                    }
                }
            }

            EditorUtility.SetDirty(shared);

            return id;
        }

        /// <summary>Maps a CSV type label to its <see cref="NodeType"/>, defaulting to Dialogue.</summary>
        private static NodeType ParseType(string type) => type switch
        {
            "Dialogue" => NodeType.Dialogue,
            "Choice" => NodeType.Choice,
            "Trigger" => NodeType.Trigger,
            "Event" => NodeType.Event,
            "Wait" => NodeType.Wait,
            "Condition" => NodeType.Condition,
            "Object" => NodeType.Object,
            _ => NodeType.Dialogue,
        };

        /// <summary>Returns the CSV key, or a synthesized "id_suffix" key when none was provided.</summary>
        private static string ResolveKey(string csvKey, string id, string suffix)
            => string.IsNullOrEmpty(csvKey) ? $"{id}_{suffix}" : csvKey;

        /// <summary>Returns true when the option Data string contains the "hide" flag.</summary>
        private static bool ParseHide(string data)
        {
            foreach (string part in SplitData(data))
            {
                if (part == "hide") return true;
            }

            return false;
        }

        /// <summary>Parses the encoded condition string and appends key/value/comparison triples to the target list.</summary>
        private static void AppendConditions(List<GenericData> target, string data)
        {
            foreach (string part in SplitData(data))
            {
                if (part == "hide") continue;

                if (!TrySplitCondition(part, out string key, out string op, out string valueStr)) continue;

                if (bool.TryParse(valueStr, out bool boolValue))
                {
                    target.Add(new GenericData(key));
                    target.Add(new GenericData(boolValue));
                    target.Add(new GenericData(GenericData.DataType.Enum));
                }
                else if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    target.Add(new GenericData(key));
                    target.Add(new GenericData(floatValue));
                    target.Add(new GenericData(OpToCheckType(op)));
                }
            }
        }

        /// <summary>Parses the encoded setter string into Trigger option wrappers, adding an empty slot if none.</summary>
        private static void AppendTrigger(List<DataWrapper> options, string data)
        {
            foreach (string part in SplitData(data))
            {
                if (!TrySplitSetter(part, out string key, out char op, out string valueStr)) continue;

                if (op == '=' && bool.TryParse(valueStr, out bool boolValue))
                {
                    options.Add(new DataWrapper(new GenericData(key), new GenericData(boolValue)));
                }
                else if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    ValueSetterType setter = op switch
                    {
                        '+' => ValueSetterType.Plus,
                        '-' => ValueSetterType.Minus,
                        _ => ValueSetterType.Set,
                    };

                    options.Add(new DataWrapper(new GenericData(key), new GenericData(floatValue), new GenericData(setter)));
                }
            }

            if (options.Count == 0)
            {
                options.Add(new DataWrapper(new GenericData(GenericData.DataType.String), new GenericData(GenericData.DataType.Bool)));
            }
        }

        /// <summary>Parses the encoded event ids into Event option wrappers, adding an empty slot if none.</summary>
        private static void AppendEvent(List<DataWrapper> options, string data)
        {
            foreach (string part in SplitData(data))
            {
                options.Add(new DataWrapper(new GenericData(part)));
            }

            if (options.Count == 0)
            {
                options.Add(new DataWrapper(new GenericData(GenericData.DataType.String)));
            }
        }

        /// <summary>Decodes a Wait Data string into a Seconds or Notify(reason) option wrapper.</summary>
        private static DataWrapper MakeWait(string data)
        {
            data = data.Trim();

            if (float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
            {
                return new DataWrapper(new GenericData(WaitNode.WaitType.Seconds), new GenericData(seconds));
            }

            string reason = "";

            if (data.StartsWith("notify:")) reason = data.Substring("notify:".Length);
            else if (data != "notify") reason = data;

            return new DataWrapper(new GenericData(WaitNode.WaitType.Notify), new GenericData(reason));
        }

        /// <summary>Splits a "; "-joined Data string into trimmed, non-empty parts.</summary>
        private static IEnumerable<string> SplitData(string data)
        {
            if (string.IsNullOrEmpty(data)) yield break;

            foreach (string raw in data.Split(';'))
            {
                string part = raw.Trim();

                if (part.Length > 0) yield return part;
            }
        }

        /// <summary>Splits a condition like "gold>=10" into key, comparison operator and value; false if no operator.</summary>
        private static bool TrySplitCondition(string part, out string key, out string op, out string value)
        {
            key = op = value = "";

            string[] ops = { ">=", "<=", "==", "!=", ">", "<" };

            foreach (string candidate in ops)
            {
                int index = part.IndexOf(candidate, System.StringComparison.Ordinal);

                if (index > 0)
                {
                    key = part.Substring(0, index).Trim();
                    op = candidate;
                    value = part.Substring(index + candidate.Length).Trim();

                    return true;
                }
            }

            return false;
        }

        /// <summary>Splits a setter like "gold+10" into key, +/-/= operator and value; false if no operator.</summary>
        private static bool TrySplitSetter(string part, out string key, out char op, out string value)
        {
            key = value = "";
            op = '=';

            for (int i = 1; i < part.Length; i++)
            {
                char c = part[i];

                if (c == '+' || c == '-' || c == '=')
                {
                    key = part.Substring(0, i).Trim();
                    op = c;
                    value = part.Substring(i + 1).Trim();

                    return key.Length > 0;
                }
            }

            return false;
        }

        /// <summary>Maps a comparison operator string to its <see cref="ValueCheckType"/>, defaulting to Equal.</summary>
        private static ValueCheckType OpToCheckType(string op) => op switch
        {
            "<" => ValueCheckType.Less,
            "<=" => ValueCheckType.LessEqual,
            "==" => ValueCheckType.Equal,
            "!=" => ValueCheckType.NotEqual,
            ">" => ValueCheckType.Greater,
            ">=" => ValueCheckType.GreaterEqual,
            _ => ValueCheckType.Equal,
        };

        /// <summary>Assigns each node a position, using explicit CSV coordinates or a BFS-depth grid layout otherwise.</summary>
        private static void AssignPositions(ref NodeData startNode, List<NodeData> nodeList, List<LinkData> links,
            Dictionary<string, Vector2> explicitPositions)
        {
            Dictionary<string, List<string>> outgoing = new();

            foreach (LinkData link in links)
            {
                if (!outgoing.TryGetValue(link.nodeGuid, out List<string> list))
                {
                    list = new List<string>();

                    outgoing.Add(link.nodeGuid, list);
                }

                list.Add(link.targetGuid);
            }

            Dictionary<string, int> depth = new();
            Queue<string> queue = new();

            depth[startNode.guid] = 0;
            queue.Enqueue(startNode.guid);

            while (queue.Count > 0)
            {
                string guid = queue.Dequeue();

                if (!outgoing.TryGetValue(guid, out List<string> targets)) continue;

                foreach (string target in targets)
                {
                    if (depth.ContainsKey(target)) continue;

                    depth[target] = depth[guid] + 1;

                    queue.Enqueue(target);
                }
            }

            Dictionary<int, int> rowInColumn = new();

            startNode.position = explicitPositions.TryGetValue(startNode.guid, out Vector2 startPos)
                ? startPos
                : NextPosition(rowInColumn, 0);

            for (int i = 0; i < nodeList.Count; i++)
            {
                NodeData node = nodeList[i];

                if (explicitPositions.TryGetValue(node.guid, out Vector2 pos))
                {
                    node.position = pos;
                }
                else
                {
                    int column = depth.TryGetValue(node.guid, out int d) ? d : 0;

                    node.position = NextPosition(rowInColumn, column);
                }

                nodeList[i] = node;
            }
        }

        /// <summary>Returns the next free grid position in a column, advancing that column's row counter.</summary>
        private static Vector2 NextPosition(Dictionary<int, int> rowInColumn, int column)
        {
            int rowIndex = rowInColumn.TryGetValue(column, out int r) ? r : 0;

            rowInColumn[column] = rowIndex + 1;

            return new Vector2(column * 280, rowIndex * 170);
        }
    }
}
