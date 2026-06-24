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

        private class LocaleColumn
        {
            public string code;
            public int speakerColumn;
            public int textColumn;
        }

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

        private class Ctx
        {
            public IReadOnlyList<StringTableCollection> collections;
            public readonly Dictionary<string, StringTableCollection> resolvedTables = new();
            public readonly List<string> warnings = new();
            public readonly Dictionary<string, List<DataWrapper>> existingObjects = new();
        }

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

            Ctx ctx = new()
            {
                collections = LocalizationEditorSettings.GetStringTableCollections(),
            };

            foreach (NodeData node in so.EditorNodes)
            {
                if (node.type == NodeType.Object) ctx.existingObjects[node.guid] = node.EditorOptionDatas;
            }

            // Resolve every referenced Speaker/Text table up front (may prompt to create or abort)
            // so we never half-write the SO when a table is missing.
            if (!ResolveTables(so, nodes, ctx, out message)) return false;

            Build(so, nodes, ctx);

            if (ctx.warnings.Count > 0) message = "Imported with warnings:\n- " + string.Join("\n- ", ctx.warnings);

            return true;
        }

        #region Parse
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

        private static LocaleColumn GetOrAdd(Dictionary<string, LocaleColumn> map, string code)
        {
            if (!map.TryGetValue(code, out LocaleColumn column))
            {
                column = new LocaleColumn { code = code, speakerColumn = -1, textColumn = -1 };

                map.Add(code, column);
            }

            return column;
        }

        private static bool TryParseLocaleHeader(string name, string prefix, out string code)
        {
            code = "";

            if (!name.StartsWith(prefix + "[") || !name.EndsWith("]")) return false;

            int start = prefix.Length + 1;

            code = name.Substring(start, name.Length - start - 1);

            return true;
        }

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

        private static bool IsEmptyRow(List<string> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (!string.IsNullOrEmpty(cells[i])) return false;
            }

            return true;
        }

        private static Row ReadRow(List<string> cells, List<LocaleColumn> localeColumns)
        {
            Row row = new()
            {
                id = Cell(cells, COL_ID),
                type = Cell(cells, COL_TYPE),
                next = Cell(cells, COL_NEXT),
                // Content fields keep their leading/trailing whitespace; structural fields are trimmed.
                // A cell that is only whitespace counts as empty.
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

        private static string Cell(List<string> cells, int index, bool trim = true)
        {
            if (index < 0 || index >= cells.Count) return "";

            string value = cells[index];

            return trim ? value.Trim() : value;
        }

        /// <summary>Normalizes a content cell: a whitespace-only value is treated as empty, while a value
        /// with real text keeps its surrounding whitespace intact.</summary>
        private static string Content(string value) => string.IsNullOrWhiteSpace(value) ? "" : value;
        #endregion

        #region Build
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

            foreach ((string source, int port, string target) in linkRequests)
            {
                if (!validIds.Contains(target)) continue;

                links.Add(new LinkData { nodeGuid = source, targetGuid = target, outPortIndex = port });
            }

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

        private static void AddLink(List<(string, int, string)> requests, string source, int port, string target)
        {
            if (!string.IsNullOrEmpty(target)) requests.Add((source, port, target));
        }

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
                    // CSV can't carry asset references. Reuse the existing node's object slots
                    // (matched by id) so assignments survive; for a brand-new object node create one
                    // empty slot per "Asset" sub-row so the editor shows the right number to fill in.
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
        #endregion

        #region Localization
        /// <summary>
        /// Resolves every Speaker/Text table referenced by content rows before the SO is touched.
        /// An empty table name (on a row that has text) aborts the import; an unknown table name
        /// prompts the user to create it (with a folder picker) or abort.
        /// </summary>
        private static bool ResolveTables(DialogueSO so, List<Row> nodes, Ctx ctx, out string error)
        {
            error = "";

            // Empty table name on a content row? Offer the SO's default Speaker/Text table (if any)
            // before the empty-name check below would abort the import.
            ApplyDefaultTables(so, nodes, ctx);

            List<(string name, string nodeId, string kind)> needed = new();

            foreach (Row row in nodes)
            {
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

            // 1) Empty table name on a row that carries text -> warn and abort (before creating anything).
            foreach ((string name, string nodeId, string kind) in needed)
            {
                if (string.IsNullOrEmpty(name))
                {
                    error = $"Node '{nodeId}' has {kind} text but its {kind} table name is empty.\nImport aborted.";

                    return false;
                }
            }

            // 2) Unknown table name -> ask to create (with folder picker), else abort.
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

        /// <summary>
        /// Fills empty Speaker/Text table names on content rows with the SO's default table, asking the
        /// user once per kind whether to use it. Rows left empty (no default, or the user skips) fall
        /// through to the empty-name abort in <see cref="ResolveTables"/>.
        /// </summary>
        private static void ApplyDefaultTables(DialogueSO so, List<Row> nodes, Ctx ctx)
        {
            string speakerDefault = GetDefaultTableName(so.SpeakerTable, ctx);
            string textDefault = GetDefaultTableName(so.TextTable, ctx);

            if (string.IsNullOrEmpty(speakerDefault) && string.IsNullOrEmpty(textDefault)) return;

            // Cache the "use default 'X'?" answer per table name: the same default table is asked once
            // (no matter how many nodes need it), while distinct default tables are still asked separately.
            Dictionary<string, bool> decisions = new();

            foreach (Row row in nodes)
            {
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

        /// <summary>Returns <paramref name="defaultName"/> when a content row's table name is empty and
        /// the user agrees to fall back to the SO default; otherwise the original name. The answer is
        /// cached in <paramref name="decisions"/> by table name, so a table already decided by an earlier
        /// node is not asked again while a different default table still prompts.</summary>
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

        /// <summary>Resolves the SO's default <see cref="LocalizedStringTable"/> to a collection name,
        /// matching by GUID against the known collections when the reference carries no name.</summary>
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

        private static StringTableCollection ResolveTable(Ctx ctx, string name)
            => !string.IsNullOrEmpty(name) && ctx.resolvedTables.TryGetValue(name, out StringTableCollection collection) ? collection : null;

        private static StringTableCollection FindCollection(IReadOnlyList<StringTableCollection> collections, string name)
        {
            for (int i = 0; i < collections.Count; i++)
            {
                if (collections[i].TableCollectionName == name) return collections[i];
            }

            return null;
        }

        private static string ToProjectRelative(string absolute)
        {
            absolute = absolute.Replace('\\', '/');

            string dataPath = Application.dataPath.Replace('\\', '/');

            if (absolute == dataPath) return "Assets";
            if (absolute.StartsWith(dataPath + "/")) return "Assets" + absolute.Substring(dataPath.Length);

            return null;
        }

        /// <summary>Speaker/Text table descriptor (name + guid) for a node option slot.</summary>
        private static DataWrapper MakeTableEntry(StringTableCollection collection, string key, Dictionary<string, string> values, out long id)
        {
            id = WriteEntry(collection, key, values);

            return MakeTableName(collection, id != 0);
        }

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
        #endregion

        #region Data parsing
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

        private static string ResolveKey(string csvKey, string id, string suffix)
            => string.IsNullOrEmpty(csvKey) ? $"{id}_{suffix}" : csvKey;

        private static bool ParseHide(string data)
        {
            foreach (string part in SplitData(data))
            {
                if (part == "hide") return true;
            }

            return false;
        }

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

        private static IEnumerable<string> SplitData(string data)
        {
            if (string.IsNullOrEmpty(data)) yield break;

            foreach (string raw in data.Split(';'))
            {
                string part = raw.Trim();

                if (part.Length > 0) yield return part;
            }
        }

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
        #endregion

        #region Layout
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

        private static Vector2 NextPosition(Dictionary<int, int> rowInColumn, int column)
        {
            int rowIndex = rowInColumn.TryGetValue(column, out int r) ? r : 0;

            rowInColumn[column] = rowIndex + 1;

            return new Vector2(column * 280, rowIndex * 170);
        }
        #endregion
    }
}
