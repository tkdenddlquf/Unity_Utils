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
            public readonly Dictionary<string, StringTableCollection> resolvedTables = new();
            public readonly List<string> warnings = new();
            public readonly HashSet<string> skippedTables = new();
            public readonly List<(string name, string folder)> plannedTables = new();
            public readonly Dictionary<string, bool> entryDecisions = new();
            public readonly Dictionary<string, string> reservedIds = new();
            public readonly Dictionary<string, long> reservedKeys = new();
            public readonly List<string> createdAssets = new();
            public bool reuseStart;
        }

        public static bool Preview(DialogueSO so, string csv, out string message)
        {
            List<List<string>> rows = CsvUtility.FromCsv(csv);

            if (rows.Count < 1)
            {
                message = "CSV is empty.";
                return false;
            }

            if (!ValidateHeader(rows[0], out message)) return false;

            List<LocaleColumn> languages = ParseHeader(rows[0]);
            List<Row> nodes;

            try
            {
                nodes = ParseRows(rows, languages, rows[0], false);
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

            if (!ValidateHeader(rows[0], out message)) return false;

            List<LocaleColumn> localeColumns = ParseHeader(rows[0]);
            Ctx ctx = new() { collections = LocalizationEditorSettings.GetStringTableCollections() };

            try
            {
                List<Row> nodes = ParseRows(rows, localeColumns, rows[0], true);
                ResolveIds(nodes, ctx);
                ValidateRows(so, nodes, ctx);
                ResolveTables(so, nodes, ctx);

                // No graph or localization assets are changed until every correction and
                // missing-data decision has been collected.
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Import Dialogue CSV");
                try
                {
                    CreatePlannedTables(ctx);
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
            catch (OperationCanceledException)
            {
                message = "CSV import canceled. No dialogue graph changes were applied.";
                return false;
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
            return true;
        }

        private static List<Row> ParseRows(List<List<string>> rows, List<LocaleColumn> localeColumns, List<string> header, bool interactive)
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

                if (interactive) ValidatePresence(cells, localeColumns, r + 1);

                Row row = ReadRow(cells, localeColumns, speakerGuidColumn, textGuidColumn, speakerIdColumn, textIdColumn);
                row.line = r + 1;

                if (interactive)
                {
                    string expected = current?.type switch
                    {
                        "Choice" => "Option",
                        "Condition" => "Branch",
                        "Command" => "Instruction",
                        "Event" => "Instruction",
                        _ => "",
                    };

                    while (!IsNodeType(row.type) && (row.type != expected || string.IsNullOrEmpty(expected)))
                    {
                        string reason = IsDetailType(row.type) ? "This detail row has no matching parent node, or its type is not supported by that parent." : "Unsupported node type. Enter Start, Dialogue, Choice, Trigger, Event, Wait, Condition, or Command.";

                        if (!RepairValue(row, "Type", ref row.type, reason, value => IsNodeType(value) || value == expected && !string.IsNullOrEmpty(expected)))
                        {
                            // A skipped parent must not absorb subsequent detail rows.
                            current = null;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(row.type)) continue;
                }

                if (IsDetailType(row.type))
                {
                    if (current != null)
                    {
                        row.parentId = current.id;
                        row.detailIndex = current.children.Count;
                        current.children.Add(row);
                    }
                    else if (interactive) throw new FormatException($"CSV row {row.line}: detail has no parent node.");
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
            for (int i = 0; i < columns.Count; i++)
            {
                LocaleColumn column = columns[i];
                ValidatePresenceCell(cells, column.speakerPresentColumn, line, $"SpeakerPresent[{column.code}]");
                ValidatePresenceCell(cells, column.textPresentColumn, line, $"TextPresent[{column.code}]");
            }
        }

        private static void ValidatePresenceCell(List<string> cells, int column, int line, string name)
        {
            if (column < 0) return;

            while (cells.Count <= column) cells.Add("");

            string value = cells[column].Trim();

            if (value == "0" || value == "1") return;

            Row row = new() { line = line, id = Cell(cells, COL_ID), type = Cell(cells, COL_TYPE) };

            if (!RepairValue(row, name, ref value, "Localization presence must be 0 (missing) or 1 (entry exists).", corrected => corrected == "0" || corrected == "1")) value = "0";

            cells[column] = value;
        }

        private static bool IsDetailType(string type) => type == "Option" || type == "Branch" || type == "Instruction" || type == "Asset";

        private static bool IsNodeType(string type) => type == "Start" || type == "Dialogue" || type == "Choice" || type == "Trigger" || type == "Event" || type == "Wait" || type == "Condition" || type == "Command";

        private static void ResolveIds(List<Row> nodes, Ctx ctx)
        {
            HashSet<string> seen = new();

            for (int i = 0; i < nodes.Count; i++)
            {
                Row row = nodes[i];
                bool missing = string.IsNullOrWhiteSpace(row.id);

                if (missing)
                {
                    bool generate = EditorUtility.DisplayDialog("Missing Node ID", $"CSV row {row.line}: {row.type} node has no ID.\n\nGenerate a unique ID? (No skips this node and its details.)", "Generate", "Skip Node");

                    if (!generate)
                    {
                        ctx.warnings.Add($"CSV row {row.line}: skipped {row.type} node without ID.");
                        nodes.RemoveAt(i--);
                        continue;
                    }

                    row.id = Guid.NewGuid().ToString();
                }

                if (!Guid.TryParse(row.id, out _) || seen.Contains(row.id))
                {
                    string issue = seen.Contains(row.id) ? "Duplicate node ID. Enter a different GUID." : "Invalid node ID. Enter a valid GUID.";

                    if (!RepairValue(row, "ID", ref row.id, issue, value => Guid.TryParse(value, out _) && !seen.Contains(value)))
                    {
                        ctx.warnings.Add($"CSV row {row.line}: skipped {row.type} node with invalid ID.");
                        nodes.RemoveAt(i--);
                        continue;
                    }
                }

                seen.Add(row.id);

                foreach (Row child in row.children) child.parentId = row.id;
            }
        }

        private static bool RepairValue(Row row, string field, ref string value, string description, Func<string, bool> valid)
        {
            while (true)
            {
                DialogueCsvRepairWindow.Decision decision = DialogueCsvRepairWindow.Request(row.line, string.IsNullOrEmpty(row.id) ? row.parentId : row.id, row.type, field, value, description, out string correction);

                if (decision == DialogueCsvRepairWindow.Decision.Cancel) throw new OperationCanceledException();

                if (decision == DialogueCsvRepairWindow.Decision.Skip)
                {
                    value = "";
                    return false;
                }

                correction = correction.Trim();

                if (valid(correction))
                {
                    value = correction;
                    return true;
                }

                value = correction;
                description = $"'{correction}' is not valid for {field}. Please enter a valid replacement.";
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

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];

                if (row.type != "Start") continue;

                if (!foundStart)
                {
                    foundStart = true;
                    continue;
                }

                string type = row.type;

                if (!RepairValue(row, "Type", ref type, "Multiple Start nodes. Change the extra node to a supported non-Start type, or skip it.", value => IsNodeType(value) && value != "Start"))
                {
                    ctx.warnings.Add($"CSV row {row.line}: skipped duplicate Start node.");
                    rows.RemoveAt(i--);
                    continue;
                }

                row.type = type;
            }

            if (!foundStart)
            {
                ctx.reuseStart = !EditorUtility.DisplayDialog("Missing Start Node", "The CSV has no Start node. Create a new one?\n\nChoosing 'Keep Existing' preserves the asset's current Start node and connects it to the first imported node.", "Create Start", "Keep Existing");
                ctx.warnings.Add(ctx.reuseStart ? "Kept the existing Start node because the CSV had none." : "Created a Start node because the CSV had none.");
            }

            if (ctx.reuseStart)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].id != so.EditorStartNode.guid) continue;

                    if (!EditorUtility.DisplayDialog("Start ID Conflict", $"CSV row {rows[i].line} uses the existing Start node's ID. A new Start node is required to avoid duplicate IDs. Create one?", "Create Start", "Cancel Import")) throw new OperationCanceledException();

                    ctx.reuseStart = false;
                    break;
                }
            }

            HashSet<string> validIds = new();

            foreach (Row row in rows) validIds.Add(row.id);

            if (ctx.reuseStart) validIds.Add(so.EditorStartNode.guid);

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                ValidateNodeFields(row, ctx);
                ValidateTarget(row, validIds, ctx);
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
                    string type = child.type;

                    if (child.type != childType)
                    {
                        if (string.IsNullOrEmpty(childType) || !RepairValue(child, "Type", ref type, $"Detail row does not belong under {row.type} node {row.id}. Expected {childType}.", value => value == childType))
                        {
                            ctx.warnings.Add($"CSV row {child.line}: skipped incompatible detail row of node '{row.id}'.");
                            row.children.RemoveAt(j--);
                            continue;
                        }

                        child.type = type;
                    }

                    child.detailIndex = j;
                    ValidateNodeFields(child, ctx);
                    ValidateTarget(child, validIds, ctx);
                }
            }
        }

        private static void ValidateNodeFields(Row row, Ctx ctx)
        {
            ValidateEntryId(row, "SpeakerEntryId", ref row.speakerIdRaw, out row.speakerId, ctx);
            ValidateEntryId(row, "TextEntryId", ref row.textIdRaw, out row.textId, ctx);
            ValidateTableGuid(row, "SpeakerTableGuid", ref row.speakerGuid, ctx);
            ValidateTableGuid(row, "TextTableGuid", ref row.textGuid, ctx);
            ValidatePosition(row, "X", ref row.posX, ctx);
            ValidatePosition(row, "Y", ref row.posY, ctx);

            Func<string, bool> validData = row.type switch
            {
                "Option" => value => IsValidConditions(value, true),
                "Branch" => value => IsValidConditions(value, false),
                "Trigger" => value => IsValidTrigger(value),
                "Instruction" => value => IsValidArguments(value),
                _ => null,
            };

            if (validData == null || validData(row.data)) return;

            if (!RepairValue(row, "Data", ref row.data, "Malformed data expression. Check field IDs, operators, value types and argument format.", validData))
                ctx.warnings.Add($"CSV row {row.line}: skipped invalid Data for node '{row.id}'.");
        }

        private static void ValidateEntryId(Row row, string field, ref string value, out long id, Ctx ctx)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                id = 0;
                return;
            }

            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || id <= 0)
            {
                if (!RepairValue(row, field, ref value, "Entry ID must be a positive integer, or empty when not assigned.", input => string.IsNullOrEmpty(input) || long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed > 0))
                    ctx.warnings.Add($"CSV row {row.line}: skipped invalid {field}.");
            }

            id = string.IsNullOrEmpty(value) ? 0 : long.Parse(value, CultureInfo.InvariantCulture);
        }

        private static void ValidateTableGuid(Row row, string field, ref string value, Ctx ctx)
        {
            if (string.IsNullOrEmpty(value) || Guid.TryParse(value, out _)) return;

            if (!RepairValue(row, field, ref value, "Table GUID is invalid. Enter an existing GUID or leave it blank to resolve by table name.", input => string.IsNullOrEmpty(input) || Guid.TryParse(input, out _)))
                ctx.warnings.Add($"CSV row {row.line}: ignored invalid {field}.");
        }

        private static void ValidatePosition(Row row, string field, ref string value, Ctx ctx)
        {
            if (string.IsNullOrEmpty(value) || float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) && IsFinite(number)) return;

            if (!RepairValue(row, field, ref value, "Position must be a finite number, or blank for automatic layout.", input => string.IsNullOrEmpty(input) || float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float candidate) && IsFinite(candidate)))
                ctx.warnings.Add($"CSV row {row.line}: using automatic position for invalid {field}.");
        }

        private static void ValidateTarget(Row row, HashSet<string> ids, Ctx ctx)
        {
            if (string.IsNullOrEmpty(row.next) || ids.Contains(row.next)) return;

            if (!RepairValue(row, "Next", ref row.next, $"Linked target '{row.next}' does not exist among imported nodes. Enter an existing node GUID or skip this link.", ids.Contains))
                ctx.warnings.Add($"CSV row {row.line}: skipped link to a missing node.");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsValidConditions(string data, bool allowHide)
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

        private static bool IsValidTrigger(string data)
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

        private static bool IsValidArguments(string data)
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

            while (string.IsNullOrEmpty(name) || (!ctx.resolvedTables.ContainsKey(name) && !ctx.skippedTables.Contains(name) && !HasTablePlan(ctx, name) && FindCollection(ctx.collections, name) == null))
            {
                if (string.IsNullOrEmpty(name))
                {
                    if (!RepairValue(owner, kind + "Table", ref name, $"Node '{owner.id}' contains {kind} references but no table name. Enter an existing or new table name, or skip this localization data.", value => !string.IsNullOrWhiteSpace(value)))
                    {
                        ClearEntry(entry, speaker);
                        ctx.warnings.Add($"CSV row {entry.line}: skipped {kind} data without a table.");
                        return;
                    }

                    if (speaker) owner.speakerTable = name;
                    else owner.textTable = name;
                }

                StringTableCollection existing = FindCollection(ctx.collections, name);

                if (existing != null)
                {
                    ctx.resolvedTables[name] = existing;
                    break;
                }

                int decision = EditorUtility.DisplayDialogComplex("Missing Localization Table", $"CSV row {entry.line}, node '{owner.id}' ({owner.type}):\n{kind} table '{name}' does not exist.\n\nCreate the table, correct its name, or skip this localization data?", "Create", "Skip Data", "Correct Name");

                if (decision == 1)
                {
                    ctx.skippedTables.Add(name);
                    break;
                }

                if (decision == 2)
                {
                    if (!RepairValue(owner, kind + "Table", ref name, "Enter the correct String Table name.", value => !string.IsNullOrWhiteSpace(value)))
                    {
                        ClearEntry(entry, speaker);
                        return;
                    }

                    if (speaker) owner.speakerTable = name;
                    else owner.textTable = name;

                    continue;
                }

                string absolute = EditorUtility.SaveFolderPanel($"Folder for table '{name}'", Application.dataPath, "");
                string folder = string.IsNullOrEmpty(absolute) ? null : ToProjectRelative(absolute);

                if (folder == null)
                {
                    ctx.skippedTables.Add(name);
                    ctx.warnings.Add($"CSV row {entry.line}: skipped {kind} table '{name}' (folder selection canceled or outside Assets).");
                }
                else ctx.plannedTables.Add((name, folder));
            }

            if (ctx.skippedTables.Contains(name))
            {
                ClearEntry(entry, speaker);
                ctx.warnings.Add($"CSV row {entry.line}: skipped {kind} data for table '{name}'.");
                return;
            }

            if (!ctx.resolvedTables.ContainsKey(name) && !HasTablePlan(ctx, name))
                ctx.resolvedTables[name] = FindCollection(ctx.collections, name);
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

                if (byId != null && byId.Key != key)
                {
                    if (!RepairValue(row, kind + "Key", ref key, $"Entry ID {id} belongs to '{byId.Key}', not '{key}' in table '{tableName}'. Correct the entry key or skip its localization data.", value => value == byId.Key))
                    {
                        ClearEntry(row, speaker);
                        return;
                    }
                }
                else if (byId == null && byKey != null && byKey.Id != id)
                {
                    string correctedId = id.ToString(CultureInfo.InvariantCulture);

                    if (!RepairValue(row, kind + "EntryId", ref correctedId, $"Entry key '{key}' has ID {byKey.Id}, not {id}, in table '{tableName}'. Correct the entry ID or skip its localization data.", value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed == byKey.Id))
                    {
                        ClearEntry(row, speaker);
                        return;
                    }

                    id = byKey.Id;
                }
            }

            SharedTableData.SharedTableEntry existing = collection?.ResolveEntry(id, key);

            if (existing != null)
            {
                if (speaker)
                {
                    row.speakerId = existing.Id;
                    row.speakerKey = existing.Key;
                }
                else
                {
                    row.textId = existing.Id;
                    row.textKey = existing.Key;
                }

                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                string ownerId = string.IsNullOrEmpty(row.id) ? row.parentId : row.id;
                string suffix = row.type == "Option" ? $"opt{row.detailIndex}" : speaker ? "speaker" : "text";
                string proposed = ResolveKey(key, ownerId, suffix);

                if (!RepairValue(row, kind + "Key", ref proposed, $"Missing {kind} entry key in table '{tableName}'. Enter a new key, or skip the localization data.", value => !string.IsNullOrWhiteSpace(value)))
                {
                    ClearEntry(row, speaker);
                    return;
                }

                key = proposed;
            }

            string requestedIdKey = $"{tableName}\u001f{id}";
            string requestedNameKey = $"{tableName}\u001f{key}";

            while (id != 0 && ctx.reservedIds.TryGetValue(requestedIdKey, out string usedKey) && usedKey != key ||
                   ctx.reservedKeys.TryGetValue(requestedNameKey, out long usedId) && id != usedId && usedId != 0)
            {
                string rawId = id.ToString(CultureInfo.InvariantCulture);

                if (!RepairValue(row, kind + "EntryId", ref rawId, $"Localization entry '{key}' conflicts with an entry already planned in '{tableName}'. Use a free positive ID or leave it blank for a generated ID.", input => string.IsNullOrEmpty(input) || long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) && parsed > 0 && !ctx.reservedIds.ContainsKey($"{tableName}\u001f{parsed}")))
                {
                    ClearEntry(row, speaker);
                    return;
                }

                id = string.IsNullOrEmpty(rawId) ? 0 : long.Parse(rawId, CultureInfo.InvariantCulture);
                requestedIdKey = $"{tableName}\u001f{id}";
            }

            string decisionKey = $"{tableName}\u001f{id}\u001f{key}";

            if (!ctx.entryDecisions.TryGetValue(decisionKey, out bool create))
            {
                create = EditorUtility.DisplayDialog("Missing Localization Entry", $"CSV row {row.line}, {row.type} node '{(string.IsNullOrEmpty(row.id) ? row.parentId : row.id)}':\n{kind} entry '{key}' (ID {id}) does not exist in '{tableName}'.\n\nCreate it? Selecting Skip leaves the node but removes this missing localization reference.", "Create", "Skip Data");
                ctx.entryDecisions.Add(decisionKey, create);
            }

            if (!create)
            {
                ClearEntry(row, speaker);
                ctx.warnings.Add($"CSV row {row.line}: skipped missing {kind} entry '{key}' in '{tableName}'.");
                return;
            }

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

        private static void ClearEntry(Row row, bool speaker)
        {
            if (speaker)
            {
                row.speakerId = 0;
                row.speakerKey = "";
                row.speaker.Clear();
                row.createSpeaker = false;
            }
            else
            {
                row.textId = 0;
                row.textKey = "";
                row.text.Clear();
                row.createText = false;
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
                    row.speakerTable = ResolveDefault(row.speaker.Count > 0 || row.speakerId != 0 || !string.IsNullOrEmpty(row.speakerKey), row.speakerTable, speakerDefault, "Speaker", row.id, decisions);
                    row.textTable = ResolveDefault(row.text.Count > 0 || row.textId != 0 || !string.IsNullOrEmpty(row.textKey), row.textTable, textDefault, "Text", row.id, decisions);
                }
                else if (type == NodeType.Choice)
                {
                    row.speakerTable = ResolveDefault(row.speaker.Count > 0 || row.speakerId != 0 || !string.IsNullOrEmpty(row.speakerKey), row.speakerTable, speakerDefault, "Speaker", row.id, decisions);
                    bool hasOptionText = false;

                    foreach (Row option in row.children)
                    {
                        if (option.text.Count > 0 || option.textId != 0 || !string.IsNullOrEmpty(option.textKey))
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
