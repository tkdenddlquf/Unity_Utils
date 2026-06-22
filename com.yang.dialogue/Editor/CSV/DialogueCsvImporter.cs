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
        private const int FIXED_COLUMNS = 9;

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

            public readonly Dictionary<string, string> speaker = new();
            public readonly Dictionary<string, string> text = new();

            public readonly List<Row> children = new();
        }

        private class Ctx
        {
            public IReadOnlyList<StringTableCollection> collections;
            public StringTableCollection fallbackSpeaker;
            public StringTableCollection fallbackText;
            public readonly List<string> warnings = new();
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
                fallbackSpeaker = ResolveCollection(so.SpeakerTable),
                fallbackText = ResolveCollection(so.TextTable),
            };

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

                if (row.type == "Option" || row.type == "Branch")
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
                message = Cell(cells, COL_MESSAGE),
                data = Cell(cells, COL_DATA),
                speakerTable = Cell(cells, COL_SPEAKER_TABLE),
                textTable = Cell(cells, COL_TEXT_TABLE),
                speakerKey = Cell(cells, COL_SPEAKER_KEY),
                textKey = Cell(cells, COL_TEXT_KEY),
            };

            foreach (LocaleColumn column in localeColumns)
            {
                string speaker = Cell(cells, column.speakerColumn);
                string text = Cell(cells, column.textColumn);

                if (!string.IsNullOrEmpty(speaker)) row.speaker[column.code] = speaker;
                if (!string.IsNullOrEmpty(text)) row.text[column.code] = text;
            }

            return row;
        }

        private static string Cell(List<string> cells, int index)
            => index >= 0 && index < cells.Count ? cells[index].Trim() : "";
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

            AssignPositions(ref startNode, nodeList, links);

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

            StringTableCollection speakerCol = ResolveByNameOrFallback(ctx, row.speakerTable, ctx.fallbackSpeaker);
            StringTableCollection textCol = ResolveByNameOrFallback(ctx, row.textTable, ctx.fallbackText);

            if ((type == NodeType.Dialogue || type == NodeType.Choice) && row.speaker.Count > 0 && speakerCol == null)
                ctx.warnings.Add($"'{row.id}': speaker text skipped (no Speaker table resolved).");

            if ((type == NodeType.Dialogue || type == NodeType.Choice) && HasContent(row, true) && textCol == null)
                ctx.warnings.Add($"'{row.id}': dialogue/choice text skipped (no Text table resolved).");

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
        private static StringTableCollection ResolveCollection(LocalizedStringTable table)
        {
            if (table == null || table.IsEmpty) return null;

            return LocalizationEditorSettings.GetStringTableCollection(table.TableReference);
        }

        private static StringTableCollection ResolveByNameOrFallback(Ctx ctx, string name, StringTableCollection fallback)
        {
            if (string.IsNullOrEmpty(name)) return fallback;

            for (int i = 0; i < ctx.collections.Count; i++)
            {
                if (ctx.collections[i].TableCollectionName == name) return ctx.collections[i];
            }

            return fallback;
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
        private static void AssignPositions(ref NodeData startNode, List<NodeData> nodeList, List<LinkData> links)
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

            startNode.position = NextPosition(depth, rowInColumn, startNode.guid, 0);

            for (int i = 0; i < nodeList.Count; i++)
            {
                NodeData node = nodeList[i];

                int column = depth.TryGetValue(node.guid, out int d) ? d : 0;

                node.position = NextPosition(depth, rowInColumn, node.guid, column);

                nodeList[i] = node;
            }
        }

        private static Vector2 NextPosition(Dictionary<string, int> depth, Dictionary<int, int> rowInColumn, string guid, int column)
        {
            int rowIndex = rowInColumn.TryGetValue(column, out int r) ? r : 0;

            rowInColumn[column] = rowIndex + 1;

            return new Vector2(column * 280, rowIndex * 170);
        }
        #endregion
    }
}
