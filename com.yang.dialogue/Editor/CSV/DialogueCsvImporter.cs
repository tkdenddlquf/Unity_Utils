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
            public int line;
            public string parentId;
            public int detailIndex;
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
            public string speakerIdRaw;
            public string textIdRaw;
            public long speakerId;
            public long textId;
            public bool createSpeaker;
            public bool createText;
            public readonly Dictionary<string, string> speaker = new();
            public readonly Dictionary<string, string> text = new();
            public readonly List<Row> children = new();
        }

        private class Ctx
        {
            public IReadOnlyList<StringTableCollection> collections;
            public DialogueCsvImportOptions options;
            public readonly Dictionary<string, StringTableCollection> resolvedTables = new();
            public readonly List<string> warnings = new();
            public readonly List<(string name, string folder)> plannedTables = new();
            public readonly Dictionary<string, string> reservedIds = new();
            public readonly Dictionary<string, long> reservedKeys = new();
            public readonly Dictionary<(string table, long id), (string key, int line)> requestedIds = new();
            public readonly Dictionary<(string table, string key), (long id, int line)> requestedKeys = new();
            public readonly Dictionary<(string table, long id), string> plannedRenames = new();
            public readonly List<string> createdAssets = new();
            public bool reuseStart;
        }

        public static bool Validate(DialogueSO so, string csv, DialogueCsvImportOptions options, out string message)
        {
            try
            {
                Prepare(so, csv, options, out _, out _);
                message = "";
                return true;
            }
            catch (FormatException exception)
            {
                message = exception.Message;
                return false;
            }
        }

        public static bool Import(DialogueSO so, string csv, DialogueCsvImportOptions options, out string message)
        {
            message = "";
            Ctx ctx;
            List<Row> nodes;

            try
            {
                Prepare(so, csv, options, out nodes, out ctx);

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Import Dialogue CSV");

                try
                {
                    CreatePlannedTables(ctx);
                    ApplyPlannedRenames(ctx);
                    Build(so, nodes, ctx);
                    Undo.CollapseUndoOperations(undoGroup);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    DeleteCreatedTables(ctx);
                    throw;
                }
            }
            catch (FormatException exception)
            {
                message = exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                message = $"CSV import failed: {exception.Message}";
                return false;
            }

            if (ctx.warnings.Count > 0) message = "Imported with warnings:\n- " + string.Join("\n- ", ctx.warnings);

            return true;
        }

        private static void Prepare(DialogueSO so, string csv, DialogueCsvImportOptions options, out List<Row> nodes, out Ctx ctx)
        {
            ctx = new Ctx { collections = LocalizationEditorSettings.GetStringTableCollections(), options = options };
            List<List<string>> rows = CsvUtility.FromCsv(csv, out List<int> lines);

            if (rows.Count == 0) throw new FormatException("CSV is empty.");

            if (!ValidateHeader(rows[0], out string error)) throw new FormatException(error);

            List<LocaleColumn> columns = ParseHeader(rows[0]);
            nodes = ParseRows(rows, columns, rows[0], lines);
            ResolveIds(nodes, ctx);
            ValidateRows(so, nodes, ctx);
            ResolveTables(so, nodes, ctx);
            ValidateLocalizedValues(nodes);
        }

        private static bool ValidateHeader(List<string> header, out string error)
        {
            string[] required = { "ID", "Type", "Next", "Message", "Data", "SpeakerTable", "TextTable", "SpeakerKey", "TextKey", "X", "Y" };

            for (int i = 0; i < required.Length; i++)
            {
                if (i < header.Count && header[i] == required[i]) continue;

                error = $"Invalid CSV header: column {i + 1} must be '{required[i]}'.";
                return false;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);

            for (int i = 0; i < header.Count; i++)
            {
                if (seen.Add(header[i])) continue;

                error = $"CSV header '{header[i]}' is duplicated.";
                return false;
            }

            for (int i = FIXED_COLUMNS; i < header.Count; i++)
            {
                string name = header[i];

                if (name == "SpeakerTableGuid" || name == "TextTableGuid" || name == "SpeakerEntryId" || name == "TextEntryId") continue;

                if (TryParseLocaleHeader(name, "Speaker", out _) || TryParseLocaleHeader(name, "Text", out _) ||
                    TryParseLocaleHeader(name, "SpeakerPresent", out _) || TryParseLocaleHeader(name, "TextPresent", out _)) continue;

                error = $"Unknown CSV header '{name}' in column {i + 1}.";
                return false;
            }

            error = "";
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
            return !string.IsNullOrWhiteSpace(code);
        }

        private static List<Row> ParseRows(List<List<string>> rows, List<LocaleColumn> localeColumns, List<string> header, List<int> lines)
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

                ValidatePresence(cells, localeColumns, lines[r]);

                Row row = ReadRow(cells, localeColumns, speakerGuidColumn, textGuidColumn, speakerIdColumn, textIdColumn);
                row.line = lines[r];

                {
                    string expected = current?.type switch
                    {
                        "Choice" => "Option",
                        "Condition" => "Branch",
                        "Command" => "Instruction",
                        "Event" => "Instruction",
                        _ => "",
                    };

                    if (!IsNodeType(row.type) && (row.type != expected || string.IsNullOrEmpty(expected)))
                        throw new FormatException($"CSV row {row.line}, Type: unsupported node or detail type '{row.type}' for the current parent.");
                }

                if (IsDetailType(row.type))
                {
                    if (current != null)
                    {
                        row.parentId = current.id;
                        row.detailIndex = current.children.Count;
                        current.children.Add(row);
                    }
                    else throw new FormatException($"CSV row {row.line}: detail has no parent node.");
                }
                else
                {
                    nodes.Add(row);
                    current = row;
                }
            }

            return nodes;
        }

        private static void ValidatePresence(List<string> cells, List<LocaleColumn> columns, int line)
        {
            string type = Cell(cells, COL_TYPE).Trim();

            for (int i = 0; i < columns.Count; i++)
            {
                LocaleColumn column = columns[i];
                ValidatePresenceCell(cells, column.speakerPresentColumn, line, $"SpeakerPresent[{column.code}]", RequiresPresence(type, true));
                ValidatePresenceCell(cells, column.textPresentColumn, line, $"TextPresent[{column.code}]", RequiresPresence(type, false));
            }
        }

        private static void ValidatePresenceCell(List<string> cells, int column, int line, string name, bool required)
        {
            if (column < 0) return;

            string value = Cell(cells, column).Trim();

            if (!required && string.IsNullOrEmpty(value)) return;
            if (value == "0" || value == "1") return;

            throw new FormatException($"CSV row {line}, {name}: expected 0 or 1, received '{value}'.");
        }

        internal static bool RequiresPresence(string type, bool speaker)
            => speaker ? type == "Dialogue" || type == "Choice" : type == "Dialogue" || type == "Option";

        internal static bool IsDetailType(string type) => type == "Option" || type == "Branch" || type == "Instruction" || type == "Asset";

        internal static bool IsNodeType(string type) => type == "Start" || type == "Dialogue" || type == "Choice" || type == "Trigger" || type == "Event" || type == "Wait" || type == "Condition" || type == "Command";

        private static void ResolveIds(List<Row> nodes, Ctx ctx)
        {
            HashSet<string> seen = new();

            foreach (Row row in nodes)
            {
                if (!Guid.TryParse(row.id, out _)) throw new FormatException($"CSV row {row.line}, ID: enter a valid GUID.");

                if (!seen.Add(row.id)) throw new FormatException($"CSV row {row.line}, ID: duplicate node ID '{row.id}'.");

                foreach (Row child in row.children) child.parentId = row.id;
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
                speakerIdRaw = Cell(cells, speakerIdColumn),
                textIdRaw = Cell(cells, textIdColumn),
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

        private static string Cell(List<string> cells, int index, bool trim = true)
        {
            if (index < 0 || index >= cells.Count) return "";

            string value = cells[index];

            return trim ? value.Trim() : value;
        }

        private static string Content(string value) => string.IsNullOrWhiteSpace(value) ? "" : value;

        private static void ValidateRows(DialogueSO so, List<Row> rows, Ctx ctx)
        {
            bool foundStart = false;

            foreach (Row row in rows)
            {
                if (row.type != "Start") continue;

                if (foundStart) throw new FormatException($"CSV row {row.line}, Type: more than one Start node.");

                foundStart = true;
            }

            ctx.reuseStart = !foundStart && ctx.options.keepStart;

            if (ctx.reuseStart && string.IsNullOrEmpty(so.EditorStartNode.guid))
                throw new FormatException("Cannot keep the existing Start node because it has no ID.");

            HashSet<string> validIds = new();

            foreach (Row row in rows) validIds.Add(row.id);

            if (ctx.reuseStart)
            {
                if (!validIds.Add(so.EditorStartNode.guid))
                    throw new FormatException("The existing Start node ID is used by an imported node. Select Create Start instead.");
            }

            foreach (Row row in rows)
            {
                ValidateNodeFields(row);
                ValidateTarget(row, validIds);
                string childType = row.type switch
                {
                    "Choice" => "Option",
                    "Condition" => "Branch",
                    "Command" => "Instruction",
                    "Event" => "Instruction",
                    _ => "",
                };

                for (int j = 0; j < row.children.Count; j++)
                {
                    Row child = row.children[j];

                    if (child.type != childType)
                        throw new FormatException($"CSV row {child.line}, Type: expected {childType} under {row.type}.");

                    child.detailIndex = j;
                    ValidateNodeFields(child);
                    ValidateTarget(child, validIds);
                }
            }
        }

        private static void ValidateNodeFields(Row row)
        {
            row.speakerId = ValidateEntryId(row, "SpeakerEntryId", row.speakerIdRaw);
            row.textId = ValidateEntryId(row, "TextEntryId", row.textIdRaw);
            ValidateTableGuid(row, "SpeakerTableGuid", row.speakerGuid);
            ValidateTableGuid(row, "TextTableGuid", row.textGuid);
            ValidatePosition(row, "X", row.posX);
            ValidatePosition(row, "Y", row.posY);

            Func<string, bool> validData = row.type switch
            {
                "Option" => value => IsValidConditions(value, true),
                "Branch" => value => IsValidConditions(value, false),
                "Trigger" => value => IsValidTrigger(value),
                "Instruction" => value => IsValidArguments(value),
                _ => null,
            };

            if (validData != null && !validData(row.data))
                throw new FormatException($"CSV row {row.line}, Data: malformed expression for {row.type}.");
        }

        private static long ValidateEntryId(Row row, string field, string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) || id <= 0)
                throw new FormatException($"CSV row {row.line}, {field}: expected a positive integer or an empty value.");

            return id;
        }

        private static void ValidateTableGuid(Row row, string field, string value)
        {
            if (!string.IsNullOrEmpty(value) && !Guid.TryParse(value, out _))
                throw new FormatException($"CSV row {row.line}, {field}: invalid table GUID.");
        }

        private static void ValidatePosition(Row row, string field, string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float position) || !IsFinite(position))
                throw new FormatException($"CSV row {row.line}, {field}: enter a finite number or leave blank for automatic layout.");
        }

        private static void ValidateTarget(Row row, HashSet<string> ids)
        {
            if (!string.IsNullOrEmpty(row.next) && !ids.Contains(row.next))
                throw new FormatException($"CSV row {row.line}, Next: target '{row.next}' does not exist in the import.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool IsValidConditions(string data, bool allowHide)
        {
            foreach (string part in SplitData(data))
            {
                if (part == "hide" && allowHide) continue;

                if (!TrySplitCondition(part, out string key, out string op, out string value)) return false;

                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fieldId) || fieldId <= 0) return false;

                if (bool.TryParse(value, out _))
                {
                    if (op != "==") return false;
                }
                else if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !IsFinite(number)) return false;
            }

            return true;
        }

        internal static bool IsValidTrigger(string data)
        {
            foreach (string part in SplitData(data))
            {
                if (!TrySplitSetter(part, out string key, out char op, out string value)) return false;

                if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fieldId) || fieldId <= 0) return false;

                if (bool.TryParse(value, out _))
                {
                    if (op != '=') return false;
                }
                else if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || !IsFinite(number)) return false;
            }

            return true;
        }

        internal static bool IsValidArguments(string data)
        {
            HashSet<int> fields = new();

            foreach (string part in SplitData(data))
            {
                int colon = part.IndexOf(':');
                int equals = part.IndexOf('=');

                if (colon <= 0 || equals <= colon + 1) return false;

                string raw = Uri.UnescapeDataString(part.Substring(0, colon).Trim());
                string type = part.Substring(colon + 1, equals - colon - 1).Trim().ToLowerInvariant();
                string value = Uri.UnescapeDataString(part.Substring(equals + 1).Trim());

                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id <= 0 || !fields.Add(id)) return false;

                switch (type)
                {
                    case "string": break;
                    case "int": if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false; break;
                    case "long": if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false; break;
                    case "float": if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) || !IsFinite(f)) return false; break;
                    case "bool": if (!bool.TryParse(value, out _)) return false; break;
                    case "color": if (!ColorUtility.TryParseHtmlString(value, out _)) return false; break;
                    case "guid": if (!Guid.TryParse(value, out _)) return false; break;
                    case "enum": if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false; break;
                    default: return false;
                }
            }

            return true;
        }

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
                startNode = ctx.reuseStart && !string.IsNullOrEmpty(so.EditorStartNode.guid) ? so.EditorStartNode : new NodeData(NodeType.Start);

                if (firstNodeId != null) AddLink(linkRequests, startNode.guid, 0, firstNodeId);
            }

            HashSet<string> validIds = new() { startNode.guid };

            foreach (NodeData node in nodeList) validIds.Add(node.guid);

            List<string> droppedLinks = new();

            foreach ((string source, int port, string target) in linkRequests)
            {
                if (!validIds.Contains(source) || !validIds.Contains(target) || port < 0)
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

                        bool hasSpeaker = row.speaker.Count > 0 || row.speakerId != 0 || !string.IsNullOrEmpty(row.speakerKey);
                        bool hasText = row.text.Count > 0 || row.textId != 0 || !string.IsNullOrEmpty(row.textKey);
                        options.Add(MakeTableEntry(hasSpeaker ? speakerCol : null, speakerKey, row.speaker, out _, row.speakerId, row.createSpeaker));
                        options.Add(MakeEntryRef(hasSpeaker ? speakerCol : null, speakerKey, row.speakerId));
                        options.Add(MakeTableEntry(hasText ? textCol : null, textKey, row.text, out _, row.textId, row.createText));
                        options.Add(MakeEntryRef(hasText ? textCol : null, textKey, row.textId));
                        options.Add(new DataWrapper(new GenericData(row.message)));
                        ports.Add(new DataWrapper());
                        AddLink(linkRequests, row.id, 0, row.next);
                    }
                    break;
                case NodeType.Choice:
                    {
                        string speakerKey = ResolveEntryKey(speakerCol, row.speakerId, ResolveKey(row.speakerKey, row.id, "speaker"));
                        bool hasSpeaker = row.speaker.Count > 0 || row.speakerId != 0 || !string.IsNullOrEmpty(row.speakerKey);
                        options.Add(MakeTableEntry(hasSpeaker ? speakerCol : null, speakerKey, row.speaker, out _, row.speakerId, row.createSpeaker));
                        options.Add(MakeEntryRef(hasSpeaker ? speakerCol : null, speakerKey, row.speakerId));
                        options.Add(MakeTableName(textCol, HasContent(row, true)));
                        options.Add(new DataWrapper(new GenericData(row.message)));

                        for (int i = 0; i < row.children.Count; i++)
                        {
                            Row option = row.children[i];
                            string key = ResolveEntryKey(textCol, option.textId, ResolveKey(option.textKey, row.id, $"opt{i}"));
                            bool hasOption = option.text.Count > 0 || option.textId != 0 || !string.IsNullOrEmpty(option.textKey);
                            long id = hasOption ? WriteEntry(textCol, key, option.text, option.textId, option.createText) : 0;
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
            if (row.text.Count > 0 || row.textId != 0 || !string.IsNullOrEmpty(row.textKey)) return true;

            if (checkChildren)
            {
                foreach (Row child in row.children)
                {
                    if (child.text.Count > 0 || child.textId != 0 || !string.IsNullOrEmpty(child.textKey)) return true;
                }
            }

            return false;
        }

        private static void ResolveTables(DialogueSO so, List<Row> nodes, Ctx ctx)
        {
            ResolveGuidNames(nodes, ctx);
            ApplyDefaultTables(so, nodes, ctx);

            foreach (Row row in nodes)
            {
                if (row.type == "Dialogue")
                {
                    ResolveRequiredTable(row, row, true, ctx);
                    ResolveRequiredTable(row, row, false, ctx);
                }
                else if (row.type == "Choice")
                {
                    ResolveRequiredTable(row, row, true, ctx);

                    foreach (Row option in row.children) ResolveRequiredTable(row, option, false, ctx);
                }
            }

            foreach (Row row in nodes)
            {
                if (row.type == "Dialogue")
                {
                    ValidateEntry(ctx, row, row.speakerTable, true, "Speaker");
                    ValidateEntry(ctx, row, row.textTable, false, "Text");
                }
                else if (row.type == "Choice")
                {
                    ValidateEntry(ctx, row, row.speakerTable, true, "Speaker");

                    foreach (Row option in row.children) ValidateEntry(ctx, option, row.textTable, false, "Text");
                }
            }
        }

        private static void ResolveGuidNames(List<Row> rows, Ctx ctx)
        {
            foreach (Row row in rows)
            {
                ResolveGuidName(ctx, row, true);
                ResolveGuidName(ctx, row, false);
            }
        }

        private static void ResolveGuidName(Ctx ctx, Row row, bool speaker)
        {
            string guidText = speaker ? row.speakerGuid : row.textGuid;
            string name = speaker ? row.speakerTable : row.textTable;

            if (string.IsNullOrEmpty(guidText)) return;

            Guid.TryParse(guidText, out Guid guid);
            StringTableCollection collection = ctx.collections.ResolveCollection("", guid) as StringTableCollection;

            if (collection != null)
            {
                if (speaker) row.speakerTable = collection.TableCollectionName;
                else row.textTable = collection.TableCollectionName;

                return;
            }

            if (FindCollection(ctx.collections, name) != null)
            {
                ctx.warnings.Add($"CSV row {row.line}: stale { (speaker ? "Speaker" : "Text") } table GUID ignored; resolved by table name '{name}'.");
                return;
            }

            // A missing table will be offered for creation or correction below.
            ctx.warnings.Add($"CSV row {row.line}: { (speaker ? "Speaker" : "Text") } table GUID '{guidText}' was not found.");
        }

        private static void ResolveRequiredTable(Row owner, Row entry, bool speaker, Ctx ctx)
        {
            Dictionary<string, string> values = speaker ? entry.speaker : entry.text;
            long id = speaker ? entry.speakerId : entry.textId;
            string key = speaker ? entry.speakerKey : entry.textKey;

            if (values.Count == 0 && id == 0 && string.IsNullOrEmpty(key)) return;

            string kind = speaker ? "Speaker" : "Text";
            string name = speaker ? owner.speakerTable : owner.textTable;

            if (string.IsNullOrWhiteSpace(name))
                throw new FormatException($"CSV row {entry.line}, {kind}Table: table name is required.");

            if (ctx.resolvedTables.ContainsKey(name) || HasTablePlan(ctx, name)) return;

            StringTableCollection existing = FindCollection(ctx.collections, name);

            if (existing != null)
            {
                ctx.resolvedTables[name] = existing;
                return;
            }

            if (!ctx.options.createTables)
                throw new FormatException($"CSV row {entry.line}: localization table '{name}' is missing. Enable Create missing tables in the review window.");

            if (!AssetDatabase.IsValidFolder(ctx.options.tableFolder))
                throw new FormatException($"Table folder '{ctx.options.tableFolder}' does not exist in Assets.");

            ctx.plannedTables.Add((name, ctx.options.tableFolder));
        }

        private static bool HasTablePlan(Ctx ctx, string name)
        {
            for (int i = 0; i < ctx.plannedTables.Count; i++)
            {
                if (ctx.plannedTables[i].name == name) return true;
            }

            return false;
        }

        private static void ValidateEntry(Ctx ctx, Row row, string tableName, bool speaker, string kind)
        {
            Dictionary<string, string> values = speaker ? row.speaker : row.text;
            long id = speaker ? row.speakerId : row.textId;
            string key = speaker ? row.speakerKey : row.textKey;

            if (values.Count == 0 && id == 0 && string.IsNullOrEmpty(key)) return;

            StringTableCollection collection = ResolveTable(ctx, tableName);

            if (collection != null && id != 0 && !string.IsNullOrEmpty(key))
            {
                SharedTableData.SharedTableEntry byId = collection.SharedData.GetEntry(id);
                SharedTableData.SharedTableEntry byKey = collection.SharedData.GetEntry(key);

                if (byId != null && byId.Key != key && byKey != null && byKey.Id != id)
                    throw new FormatException($"CSV row {row.line}: cannot rename entry ID {id} to '{key}' in '{tableName}' because that key belongs to ID {byKey.Id}.");

                if (byId == null && byKey != null && byKey.Id != id)
                {
                    throw new FormatException($"CSV row {row.line}, {kind}EntryId: key '{key}' belongs to ID {byKey.Id}, not {id} in '{tableName}'.");
                }
            }

            SharedTableData.SharedTableEntry existing = collection?.ResolveEntry(id, key);

            if (existing != null)
            {
                if (string.IsNullOrEmpty(key)) key = existing.Key;

                RegisterEntryIdentity(ctx, tableName, existing.Id, key, row.line);

                if (id != 0 && existing.Id == id && existing.Key != key) ctx.plannedRenames[(tableName, id)] = key;

                if (speaker)
                {
                    row.speakerId = existing.Id;
                    row.speakerKey = key;
                }
                else
                {
                    row.textId = existing.Id;
                    row.textKey = key;
                }

                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                string ownerId = string.IsNullOrEmpty(row.id) ? row.parentId : row.id;
                string suffix = row.type == "Option" ? $"opt{row.detailIndex}" : speaker ? "speaker" : "text";
                key = ResolveKey(key, ownerId, suffix);
            }

            string requestedIdKey = $"{tableName}\u001f{id}";
            string requestedNameKey = $"{tableName}\u001f{key}";

            if (id != 0 && ctx.reservedIds.TryGetValue(requestedIdKey, out string usedKey) && usedKey != key ||
                ctx.reservedKeys.TryGetValue(requestedNameKey, out long usedId) && id != usedId && usedId != 0)
                throw new FormatException($"CSV row {row.line}: entry '{key}' conflicts with another planned entry in '{tableName}'.");

            if (!ctx.options.createEntries)
                throw new FormatException($"CSV row {row.line}: localization entry '{key}' is missing in '{tableName}'. Enable Create missing entries in the review window.");

            RegisterEntryIdentity(ctx, tableName, id, key, row.line);

            if (id != 0) ctx.reservedIds[$"{tableName}\u001f{id}"] = key;

            ctx.reservedKeys[$"{tableName}\u001f{key}"] = id;

            if (speaker)
            {
                row.speakerId = id;
                row.speakerKey = key;
                row.createSpeaker = true;
            }
            else
            {
                row.textId = id;
                row.textKey = key;
                row.createText = true;
            }
        }

        private static void RegisterEntryIdentity(Ctx ctx, string tableName, long id, string key, int line)
        {
            if (id != 0)
            {
                (string table, long id) idIdentity = (tableName, id);

                if (ctx.requestedIds.TryGetValue(idIdentity, out (string key, int line) previous) && previous.key != key)
                    throw new FormatException($"CSV rows {previous.line} and {line}: entry ID {id} in '{tableName}' has conflicting keys '{previous.key}' and '{key}'.");

                ctx.requestedIds[idIdentity] = (key, line);
            }

            (string table, string key) keyIdentity = (tableName, key);

            if (ctx.requestedKeys.TryGetValue(keyIdentity, out (long id, int line) previousKey) && previousKey.id != id)
                throw new FormatException($"CSV rows {previousKey.line} and {line}: localization key '{key}' in '{tableName}' has conflicting entry IDs {previousKey.id} and {id}.");

            ctx.requestedKeys[keyIdentity] = (id, line);
        }

        private static void ValidateLocalizedValues(List<Row> nodes)
        {
            Dictionary<(string table, string entry, string locale), (string value, int line)> values = new();

            foreach (Row row in nodes)
            {
                if (row.type == "Dialogue")
                {
                    ValidateLocalizedEntry(row, row.speakerTable, row.speakerKey, row.speakerId, row.speaker, values);
                    ValidateLocalizedEntry(row, row.textTable, row.textKey, row.textId, row.text, values);
                }
                else if (row.type == "Choice")
                {
                    ValidateLocalizedEntry(row, row.speakerTable, row.speakerKey, row.speakerId, row.speaker, values);

                    foreach (Row option in row.children)
                        ValidateLocalizedEntry(option, row.textTable, option.textKey, option.textId, option.text, values);
                }
            }
        }

        private static void ValidateLocalizedEntry(Row row, string tableName, string key, long id, Dictionary<string, string> translations, Dictionary<(string table, string entry, string locale), (string value, int line)> values)
        {
            if (string.IsNullOrEmpty(tableName) || id == 0 && string.IsNullOrEmpty(key)) return;

            string entryIdentity = id != 0 ? $"id:{id}" : $"key:{key}";

            foreach (KeyValuePair<string, string> translation in translations)
            {
                (string table, string entry, string locale) identity = (tableName, entryIdentity, translation.Key);

                if (values.TryGetValue(identity, out (string value, int line) previous) && previous.value != translation.Value)
                    throw new FormatException($"CSV rows {previous.line} and {row.line}: localization entry '{key}' in '{tableName}' has conflicting '{translation.Key}' translations.");

                values[identity] = (translation.Value, row.line);
            }
        }

        private static void ApplyPlannedRenames(Ctx ctx)
        {
            foreach (KeyValuePair<(string table, long id), string> rename in ctx.plannedRenames)
            {
                StringTableCollection collection = ResolveTable(ctx, rename.Key.table);
                SharedTableData shared = collection.SharedData;
                string currentKey = shared.GetKey(rename.Key.id);

                if (currentKey == rename.Value) continue;

                Undo.RecordObject(shared, "Import Dialogue CSV Localization");
                shared.RenameKey(rename.Key.id, rename.Value);

                if (shared.GetKey(rename.Key.id) != rename.Value)
                    throw new FormatException($"Could not rename localization entry ID {rename.Key.id} in '{rename.Key.table}' from '{currentKey}' to '{rename.Value}'.");

                EditorUtility.SetDirty(shared);
            }
        }

        private static void CreatePlannedTables(Ctx ctx)
        {
            // Folder selection and every user confirmation happened during preflight.
            for (int i = 0; i < ctx.plannedTables.Count; i++)
            {
                (string name, string folder) = ctx.plannedTables[i];
                StringTableCollection collection = LocalizationEditorSettings.CreateStringTableCollection(name, folder);

                if (collection == null) throw new FormatException($"Could not create String Table '{name}'.");

                ctx.resolvedTables.Add(name, collection);
                AddCreatedAsset(ctx, collection.SharedData);

                foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables) AddCreatedAsset(ctx, reference.asset);
            }
        }

        private static void AddCreatedAsset(Ctx ctx, UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);

            if (!string.IsNullOrEmpty(path) && !ctx.createdAssets.Contains(path)) ctx.createdAssets.Add(path);
        }

        private static void DeleteCreatedTables(Ctx ctx)
        {
            for (int i = ctx.createdAssets.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(ctx.createdAssets[i]);

            AssetDatabase.SaveAssets();
        }

        private static void ApplyDefaultTables(DialogueSO so, List<Row> nodes, Ctx ctx)
        {
            if (!ctx.options.useDefaultTables) return;

            string speakerDefault = GetDefaultTableName(so.SpeakerTable, ctx);
            string textDefault = GetDefaultTableName(so.TextTable, ctx);

            foreach (Row row in nodes)
            {
                if (row.type != "Dialogue" && row.type != "Choice") continue;

                bool hasSpeaker = row.speaker.Count > 0 || row.speakerId != 0 || !string.IsNullOrEmpty(row.speakerKey);
                bool hasText = row.text.Count > 0 || row.textId != 0 || !string.IsNullOrEmpty(row.textKey);

                if (row.type == "Choice")
                {
                    foreach (Row option in row.children)
                    {
                        if (option.text.Count == 0 && option.textId == 0 && string.IsNullOrEmpty(option.textKey)) continue;

                        hasText = true;
                        break;
                    }
                }

                if (hasSpeaker && string.IsNullOrEmpty(row.speakerTable)) row.speakerTable = speakerDefault;

                if (hasText && string.IsNullOrEmpty(row.textTable)) row.textTable = textDefault;
            }
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

        private static DataWrapper MakeTableEntry(StringTableCollection collection, string key, Dictionary<string, string> values, out long id, long stableId = 0, bool createMissing = false)
        {
            id = WriteEntry(collection, key, values, stableId, createMissing);

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

        private static long WriteEntry(StringTableCollection collection, string key, Dictionary<string, string> values, long stableId = 0, bool createMissing = false)
        {
            if (collection == null) return 0;

            SharedTableData shared = collection.SharedData;
            SharedTableData.SharedTableEntry entry = collection.ResolveEntry(stableId, key);

            if (entry == null && !createMissing) return 0;

            if (entry == null)
            {
                Undo.RecordObject(shared, "Import Dialogue CSV Localization");
                entry = stableId != 0 ? shared.AddKey(key, stableId) : shared.AddKey(key);

                if (entry == null) throw new FormatException($"Could not add localization entry '{key}' (ID {stableId}) to table '{collection.TableCollectionName}'.");

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
            _ => throw new FormatException($"Unsupported CSV node type '{type}'."),
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
