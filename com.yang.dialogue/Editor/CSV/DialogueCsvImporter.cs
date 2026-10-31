using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue.Editor
{
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
            public int speakerPresentColumn;
            public int textPresentColumn;
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
            public string speakerGuid;
            public string textGuid;
            public long speakerId;
            public long textId;
            public readonly Dictionary<string, string> speaker = new();
            public readonly Dictionary<string, string> text = new();
            public readonly List<Row> children = new();
        }

        private class Ctx
        {
            public IReadOnlyList<StringTableCollection> collections;
            public readonly Dictionary<string, StringTableCollection> resolvedTables = new();
            public readonly List<string> warnings = new();
        }

        public static bool Preview(DialogueSO so, string csv, out string message)
        {
            List<List<string>> rows = CsvUtility.FromCsv(csv);

            if (rows.Count < 1)
            {
                message = "CSV is empty.";
                return false;
            }

            List<LocaleColumn> languages = ParseHeader(rows[0]);
            List<Row> nodes;

            try
            {
                nodes = ParseRows(rows, languages, rows[0]);
            }
            catch (System.FormatException exception)
            {
                message = exception.Message;
                return false;
            }

            int childRows = 0;
            int textCells = 0;
            int speakerCells = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                Row node = nodes[i];
                childRows += node.children.Count;
                textCells += node.text.Count;
                speakerCells += node.speaker.Count;

                for (int j = 0; j < node.children.Count; j++)
                {
                    textCells += node.children[j].text.Count;
                    speakerCells += node.children[j].speaker.Count;
                }
            }

            message = $"Current asset: {so.EditorNodes.Count + 1} nodes, {so.EditorLinks.Count} links.\n"
                + $"CSV: {nodes.Count} nodes, {childRows} detail rows, {languages.Count} locale columns/groups.\n"
                + $"Localized cells present: speaker {speakerCells}, text {textCells}.\n\n"
                + "Import replaces this asset's nodes and links, and may update the referenced String Tables. "
                + "Counts represent CSV contents, not an exact difference preview.";
            return true;
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
            List<Row> nodes;

            try
            {
                nodes = ParseRows(rows, localeColumns, rows[0]);
            }
            catch (System.FormatException exception)
            {
                message = exception.Message;
                return false;
            }

            ResolveIds(nodes);
            Ctx ctx = new()
            {
                collections = LocalizationEditorSettings.GetStringTableCollections(),
            };

            if (!ResolveTables(so, nodes, ctx, out message)) return false;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Import Dialogue CSV");
            Build(so, nodes, ctx);
            Undo.CollapseUndoOperations(undoGroup);

            if (ctx.warnings.Count > 0) message = "Imported with warnings:\n- " + string.Join("\n- ", ctx.warnings);

            return true;
        }

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
                else if (TryParseLocaleHeader(name, "SpeakerPresent", out code)) GetOrAdd(byCode, code).speakerPresentColumn = i;
                else if (TryParseLocaleHeader(name, "TextPresent", out code)) GetOrAdd(byCode, code).textPresentColumn = i;
            }

            return new List<LocaleColumn>(byCode.Values);
        }

        private static LocaleColumn GetOrAdd(Dictionary<string, LocaleColumn> map, string code)
        {
            if (!map.TryGetValue(code, out LocaleColumn column))
            {
                column = new LocaleColumn { code = code, speakerColumn = -1, textColumn = -1, speakerPresentColumn = -1, textPresentColumn = -1 };
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

        private static List<Row> ParseRows(List<List<string>> rows, List<LocaleColumn> localeColumns, List<string> header)
        {
            List<Row> nodes = new();
            int speakerGuidColumn = header.IndexOf("SpeakerTableGuid");
            int textGuidColumn = header.IndexOf("TextTableGuid");
            int speakerIdColumn = header.IndexOf("SpeakerEntryId");
            int textIdColumn = header.IndexOf("TextEntryId");
            Row current = null;

            for (int r = 1; r < rows.Count; r++)
            {
                List<string> cells = rows[r];

                if (IsEmptyRow(cells)) continue;

                Row row = ReadRow(cells, localeColumns, speakerGuidColumn, textGuidColumn, speakerIdColumn, textIdColumn);

                if (row.type == "Option" || row.type == "Branch" || row.type == "Instruction" || row.type == "Asset")
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

        private static bool IsEmptyRow(List<string> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (!string.IsNullOrEmpty(cells[i])) return false;
            }

            return true;
        }

        private static Row ReadRow(List<string> cells, List<LocaleColumn> localeColumns, int speakerGuidColumn, int textGuidColumn, int speakerIdColumn, int textIdColumn)
        {
            long speakerId = ParseId(cells, speakerIdColumn, "SpeakerEntryId");
            long textId = ParseId(cells, textIdColumn, "TextEntryId");
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
                speakerGuid = Cell(cells, speakerGuidColumn),
                textGuid = Cell(cells, textGuidColumn),
                speakerId = speakerId,
                textId = textId,
            };

            foreach (LocaleColumn column in localeColumns)
            {
                string speaker = Cell(cells, column.speakerColumn, false);
                string text = Cell(cells, column.textColumn, false);

                if (column.speakerColumn >= 0 && (column.speakerPresentColumn >= 0 ? Cell(cells, column.speakerPresentColumn) == "1" : !string.IsNullOrEmpty(speaker))) row.speaker[column.code] = speaker;

                if (column.textColumn >= 0 && (column.textPresentColumn >= 0 ? Cell(cells, column.textPresentColumn) == "1" : !string.IsNullOrEmpty(text))) row.text[column.code] = text;
            }

            return row;
        }

        private static long ParseId(List<string> cells, int column, string label)
        {
            string value = Cell(cells, column);

            if (string.IsNullOrEmpty(value)) return 0;

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) && id != 0) return id;

            throw new System.FormatException($"Invalid {label}: '{value}'. Import aborted.");
        }

        private static string Cell(List<string> cells, int index, bool trim = true)
        {
            if (index < 0 || index >= cells.Count) return "";

            string value = cells[index];

            return trim ? value.Trim() : value;
        }

        private static string Content(string value) => string.IsNullOrWhiteSpace(value) ? "" : value;

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

                if (float.TryParse(row.posX, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) && float.TryParse(row.posY, NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) explicitPositions[row.id] = new Vector2(x, y);
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
                        string speakerKey = ResolveEntryKey(speakerCol, row.speakerId, ResolveKey(row.speakerKey, row.id, "speaker"));
                        string textKey = ResolveEntryKey(textCol, row.textId, ResolveKey(row.textKey, row.id, "text"));

                        options.Add(MakeTableEntry(speakerCol, speakerKey, row.speaker, out _, row.speakerId));
                        options.Add(MakeEntryRef(speakerCol, speakerKey, row.speakerId));
                        options.Add(MakeTableEntry(textCol, textKey, row.text, out _, row.textId));
                        options.Add(MakeEntryRef(textCol, textKey, row.textId));
                        options.Add(new DataWrapper(new GenericData(row.message)));
                        ports.Add(new DataWrapper());
                        AddLink(linkRequests, row.id, 0, row.next);
                    }
                    break;
                case NodeType.Choice:
                    {
                        string speakerKey = ResolveEntryKey(speakerCol, row.speakerId, ResolveKey(row.speakerKey, row.id, "speaker"));
                        options.Add(MakeTableEntry(speakerCol, speakerKey, row.speaker, out _, row.speakerId));
                        options.Add(MakeEntryRef(speakerCol, speakerKey, row.speakerId));
                        options.Add(MakeTableName(textCol, HasContent(row, true)));
                        options.Add(new DataWrapper(new GenericData(row.message)));

                        for (int i = 0; i < row.children.Count; i++)
                        {
                            Row option = row.children[i];
                            string key = ResolveEntryKey(textCol, option.textId, ResolveKey(option.textKey, row.id, $"opt{i}"));
                            long id = WriteEntry(textCol, key, option.text, option.textId);
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

                        if (ports.Count == 0) ports.Add(new DataWrapper(new GenericData(GenericData.DataType.String), new GenericData(GenericData.DataType.Long), new GenericData(GenericData.DataType.Bool)));
                    }

                    break;
                case NodeType.Trigger:
                    AppendTrigger(options, row.data);
                    ports.Add(new DataWrapper());
                    AddLink(linkRequests, row.id, 0, row.next);
                    break;
                case NodeType.Event:

                    for (int i = 0; i < row.children.Count; i++)
                    {
                        Row instruction = row.children[i];

                        if (instruction.type != "Instruction") continue;

                        List<GenericData> eventData = new() { new GenericData(instruction.message) };
                        AppendCommandArguments(eventData, instruction.data, ctx, row.id);
                        options.Add(new DataWrapper(eventData));
                    }

                    if (options.Count == 0) options.Add(new DataWrapper(new GenericData(GenericData.DataType.String)));

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
                case NodeType.Command:

                    for (int i = 0; i < row.children.Count; i++)
                    {
                        Row instruction = row.children[i];

                        if (instruction.type != "Instruction") continue;

                        List<GenericData> command = new()
                        {
                            new GenericData(instruction.message)
                        };
                        AppendCommandArguments(command, instruction.data, ctx, row.id);
                        options.Add(new DataWrapper(command));
                    }

                    if (options.Count == 0) options.Add(new DataWrapper(new GenericData(GenericData.DataType.String)));

                    ports.Add(new DataWrapper());
                    AddLink(linkRequests, row.id, 0, row.next);
                    break;
            }

            return node;
        }

        private static bool HasContent(Row row, bool checkChildren)
        {
            if (row.text.Count > 0 || row.textId != 0) return true;

            if (checkChildren)
            {
                foreach (Row child in row.children)
                {
                    if (child.text.Count > 0 || child.textId != 0) return true;
                }
            }

            return false;
        }

        private static bool ResolveTables(DialogueSO so, List<Row> nodes, Ctx ctx, out string error)
        {
            error = "";

            if (!ResolveGuidNames(nodes, ctx, out error)) return false;

            ApplyDefaultTables(so, nodes, ctx);
            List<(string name, string nodeId, string kind)> needed = new();

            foreach (Row row in nodes)
            {
                if (string.IsNullOrEmpty(row.id)) continue;

                NodeType type = ParseType(row.type);

                if (type == NodeType.Dialogue)
                {
                    if (row.speaker.Count > 0 || row.speakerId != 0) needed.Add((row.speakerTable, row.id, "Speaker"));

                    if (row.text.Count > 0 || row.textId != 0) needed.Add((row.textTable, row.id, "Text"));
                }
                else if (type == NodeType.Choice)
                {
                    if (row.speaker.Count > 0 || row.speakerId != 0) needed.Add((row.speakerTable, row.id, "Speaker"));

                    foreach (Row option in row.children)
                    {
                        if (option.text.Count > 0 || option.textId != 0)
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

            foreach (Row row in nodes)
            {
                if (!ValidateEntry(ctx, row.speakerTable, ref row.speakerId, ref row.speakerKey, row.id, out error)) return false;

                if (row.type == "Dialogue" && !ValidateEntry(ctx, row.textTable, ref row.textId, ref row.textKey, row.id, out error)) return false;

                if (row.type != "Choice") continue;

                foreach (Row option in row.children)

                    if (!ValidateEntry(ctx, row.textTable, ref option.textId, ref option.textKey, row.id, out error)) return false;
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

                bool create = EditorUtility.DisplayDialog("Table Not Found", $"The {kind} table '{name}' (used by node '{nodeId}') does not exist.\n\nCreate it?", "Create", "Cancel");

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

        private static bool ResolveGuidNames(List<Row> rows, Ctx ctx, out string error)
        {
            error = "";

            foreach (Row row in rows)
            {
                if (!ResolveGuidName(ctx, row.speakerGuid, ref row.speakerTable, row.speakerId, row.speakerKey, row.id, out error)) return false;

                long textId = row.textId;
                string textKey = row.textKey;

                if (row.type == "Choice")
                {
                    foreach (Row option in row.children)
                    {
                        if (option.textId == 0 && string.IsNullOrEmpty(option.textKey)) continue;

                        textId = option.textId;
                        textKey = option.textKey;
                        break;
                    }
                }

                if (!ResolveGuidName(ctx, row.textGuid, ref row.textTable, textId, textKey, row.id, out error)) return false;
            }

            return true;
        }

        private static bool ResolveGuidName(Ctx ctx, string guidText, ref string name, long id, string key, string nodeId, out string error)
        {
            error = "";

            if (string.IsNullOrEmpty(guidText) && id == 0) return true;

            System.Guid.TryParse(guidText, out System.Guid guid);
            StringTableCollection collection = ctx.collections.ResolveCollection(name, guid, id, key) as StringTableCollection;

            if (collection == null)
            {
                error = $"Localization table GUID '{guidText}' in node '{nodeId}' could not be resolved by GUID, name, or entry. Import aborted.";
                return false;
            }

            name = collection.TableCollectionName;
            return true;
        }

        private static bool ValidateEntry(Ctx ctx, string tableName, ref long id, ref string key, string nodeId, out string error)
        {
            error = "";

            if (id == 0 && string.IsNullOrEmpty(key)) return true;

            StringTableCollection collection = ResolveTable(ctx, tableName) ?? FindCollection(ctx.collections, tableName);

            if (collection == null)
            {
                if (id == 0) return true;

                error = $"Localization entry ID '{id}' in node '{nodeId}' references missing table '{tableName}'. Import aborted.";
                return false;
            }

            SharedTableData.SharedTableEntry entry = collection.ResolveEntry(id, key);

            if (entry == null)
            {
                if (id == 0) return true;

                error = $"Localization entry ID '{id}' and key '{key}' for node '{nodeId}' were not found in '{tableName}'. Import aborted.";
                return false;
            }

            id = entry.Id;
            key = entry.Key;
            return true;
        }

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
                    row.speakerTable = ResolveDefault(row.speaker.Count > 0 || row.speakerId != 0, row.speakerTable, speakerDefault, "Speaker", row.id, decisions);
                    row.textTable = ResolveDefault(row.text.Count > 0 || row.textId != 0, row.textTable, textDefault, "Text", row.id, decisions);
                }
                else if (type == NodeType.Choice)
                {
                    row.speakerTable = ResolveDefault(row.speaker.Count > 0 || row.speakerId != 0, row.speakerTable, speakerDefault, "Speaker", row.id, decisions);
                    bool hasOptionText = false;

                    foreach (Row option in row.children)
                    {
                        if (option.text.Count > 0 || option.textId != 0)
                        {
                            hasOptionText = true;
                            break;
                        }
                    }

                    row.textTable = ResolveDefault(hasOptionText, row.textTable, textDefault, "Text", row.id, decisions);
                }
            }
        }

        private static string ResolveDefault(bool hasText, string tableName, string defaultName, string kind, string nodeId, Dictionary<string, bool> decisions)
        {
            if (!hasText || !string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(defaultName)) return tableName;

            if (!decisions.TryGetValue(defaultName, out bool use))
            {
                use = EditorUtility.DisplayDialog($"Use Default {kind} Table?", $"Node '{nodeId}' has {kind} text but no {kind} table name in the CSV.\n\nUse the dialogue's default {kind} table '{defaultName}'?", "Use Default", "Skip");
                decisions[defaultName] = use;
            }

            return use ? defaultName : tableName;
        }

        private static string GetDefaultTableName(LocalizedStringTable table, Ctx ctx)
        {
            if (table == null || table.IsEmpty) return "";

            TableReference reference = table.TableReference;
            System.Guid guid = reference.TableCollectionNameGuid;

            if (guid != System.Guid.Empty) return ctx.collections.ResolveCollection("", guid)?.TableCollectionName ?? "";

            return reference.TableCollectionName ?? "";
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

        private static string ResolveEntryKey(StringTableCollection collection, long id, string key)
            => collection?.ResolveEntry(id, key)?.Key ?? key;

        private static DataWrapper MakeTableEntry(StringTableCollection collection, string key, Dictionary<string, string> values, out long id, long stableId = 0)
        {
            id = WriteEntry(collection, key, values, stableId);

            return MakeTableName(collection, id != 0);
        }

        private static DataWrapper MakeTableName(StringTableCollection collection, bool hasContent)
        {
            if (collection == null || !hasContent) return new DataWrapper(new GenericData(GenericData.DataType.String), new GenericData(GenericData.DataType.Guid));

            return new DataWrapper(new GenericData(collection.TableCollectionName), new GenericData(collection.TableCollectionNameReference.TableCollectionNameGuid));
        }

        private static DataWrapper MakeEntryRef(StringTableCollection collection, string key, long stableId = 0)
        {
            long id = collection == null ? 0 : LookupId(collection, key, stableId);

            if (id == 0) return new DataWrapper(new GenericData(GenericData.DataType.String), new GenericData(GenericData.DataType.Long));

            return new DataWrapper(new GenericData(collection.SharedData.GetEntry(id).Key), new GenericData(id));
        }

        private static long LookupId(StringTableCollection collection, string key, long stableId)
            => collection.ResolveEntry(stableId, key)?.Id ?? 0;

        private static long WriteEntry(StringTableCollection collection, string key, Dictionary<string, string> values, long stableId = 0)
        {
            if (collection == null) return 0;

            SharedTableData shared = collection.SharedData;
            SharedTableData.SharedTableEntry entry = collection.ResolveEntry(stableId, key);

            if (entry == null && values.Count == 0) return 0;

            if (entry == null)
            {
                Undo.RecordObject(shared, "Import Dialogue CSV Localization");
                entry = shared.AddKey(key);
                EditorUtility.SetDirty(shared);
            }

            long id = entry.Id;

            foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
            {
                if (reference.asset is not StringTable table) continue;

                if (!values.TryGetValue(table.LocaleIdentifier.Code, out string value)) continue;

                StringTableEntry existing = table.GetEntry(id);

                if (existing != null && existing.Value == value) continue;

                Undo.RecordObject(table, "Import Dialogue CSV Localization");

                if (existing != null) existing.Value = value;
                else table.AddEntry(id, value);
                EditorUtility.SetDirty(table);
            }

            return id;
        }

        private static NodeType ParseType(string type) => type switch
        {
            "Dialogue" => NodeType.Dialogue,
            "Choice" => NodeType.Choice,
            "Trigger" => NodeType.Trigger,
            "Event" => NodeType.Event,
            "Wait" => NodeType.Wait,
            "Condition" => NodeType.Condition,
            "Command" => NodeType.Command,
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

                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fieldId) || fieldId <= 0) continue;

                if (bool.TryParse(valueStr, out bool boolValue))
                {
                    target.Add(new GenericData(fieldId));
                    target.Add(new GenericData(boolValue));
                    target.Add(new GenericData(GenericData.DataType.Enum));
                }
                else if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    target.Add(new GenericData(fieldId));
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

                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fieldId) || fieldId <= 0) continue;

                if (op == '=' && bool.TryParse(valueStr, out bool boolValue))
                {
                    options.Add(new DataWrapper(new GenericData(fieldId), new GenericData(boolValue)));
                }
                else if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
                {
                    ValueSetterType setter = op switch
                    {
                        '+' => ValueSetterType.Plus,
                        '-' => ValueSetterType.Minus,
                        _ => ValueSetterType.Set,
                    };
                    options.Add(new DataWrapper(new GenericData(fieldId), new GenericData(floatValue), new GenericData(setter)));
                }
            }

            if (options.Count == 0) options.Add(new DataWrapper(new GenericData(GenericData.DataType.Int), new GenericData(GenericData.DataType.Bool)));
        }

        private static void AppendCommandArguments(List<GenericData> command, string data, Ctx ctx, string nodeId)
        {
            foreach (string part in SplitData(data))
            {
                int colon = part.IndexOf(':');
                int equals = part.IndexOf('=');

                if (colon <= 0 || equals <= colon + 1)
                {
                    ctx.warnings.Add($"'{nodeId}': ignored invalid command argument '{part}'.");
                    continue;
                }

                string rawFieldId = System.Uri.UnescapeDataString(part.Substring(0, colon).Trim());
                string type = part.Substring(colon + 1, equals - colon - 1).Trim().ToLowerInvariant();
                string value = System.Uri.UnescapeDataString(part.Substring(equals + 1).Trim());
                bool validFieldId = int.TryParse(rawFieldId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fieldId) && fieldId > 0;
                GenericData parsed;
                bool valid = true;

                switch (type)
                {
                    case "string":
                        parsed = new GenericData(value);
                        break;
                    case "int" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue):
                        parsed = new GenericData(intValue);
                        break;
                    case "float" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue):
                        parsed = new GenericData(floatValue);
                        break;
                    case "long" when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue):
                        parsed = new GenericData(longValue);
                        break;
                    case "bool" when bool.TryParse(value, out bool boolValue):
                        parsed = new GenericData(boolValue);
                        break;
                    case "color" when ColorUtility.TryParseHtmlString(value, out Color colorValue):
                        parsed = new GenericData((Color32)colorValue);
                        break;
                    case "guid" when System.Guid.TryParse(value, out System.Guid guidValue):
                        parsed = new GenericData(guidValue);
                        break;
                    case "enum" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int enumValue):
                        parsed = GenericData.FromEnumValue(enumValue);
                        break;
                    default:
                        parsed = default;
                        valid = false;
                        break;
                }

                if (!valid || !validFieldId)
                {
                    ctx.warnings.Add($"'{nodeId}': ignored command argument '{part}' with an invalid field id, type, or value.");
                    continue;
                }

                command.Add(new GenericData(fieldId));
                command.Add(parsed);
            }
        }

        private static DataWrapper MakeWait(string data)
        {
            data = data.Trim();

            if (float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds)) return new DataWrapper(new GenericData(WaitNode.WaitType.Seconds), new GenericData(seconds));

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

        private static void AssignPositions(ref NodeData startNode, List<NodeData> nodeList, List<LinkData> links, Dictionary<string, Vector2> explicitPositions)
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
    }
}
