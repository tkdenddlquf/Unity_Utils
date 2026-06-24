using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEngine.Localization;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Exports a <see cref="DialogueSO"/> to CSV (one row per node; Choice options, Condition
    /// branches and Object references are emitted as sub-rows below their owner).
    ///
    /// Columns: ID, Type, Next, Message, Data, then Speaker[code] / Text[code] per project locale.
    ///
    /// Data encodings:
    ///   Trigger : "key+10" (plus) / "key-5" (minus) / "key=10" (set float) / "key=true" (bool); joined by "; "
    ///   Event   : "id1; id2"
    ///   Wait    : "2.5" (seconds) or "notify" / "notify:reason"
    ///   Object  : one "Asset" sub-row per slot, object name in Message (reference-only; re-import
    ///             reuses the editor slots by id, so names are informational)
    ///   Option  : optional "hide" + conditions, e.g. "hide; gold>=10; flag==true"
    ///   Branch  : conditions, e.g. "gold>=10; hasKey==true"
    /// </summary>
    public static class DialogueCsvExporter
    {
        public static string Export(DialogueSO so)
        {
            IReadOnlyList<Locale> locales = LocalizationEditorSettings.GetLocales();
            IReadOnlyList<LocalizationTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();

            Dictionary<(string, int), string> linkMap = new();

            foreach (LinkData link in so.EditorLinks) linkMap[(link.nodeGuid, link.outPortIndex)] = link.targetGuid;

            List<List<string>> rows = new();

            // Header
            List<string> header = new() { "ID", "Type", "Next", "Message", "Data", "SpeakerTable", "TextTable", "SpeakerKey", "TextKey", "X", "Y" };

            foreach (Locale locale in locales)
            {
                string code = locale.Identifier.Code;

                header.Add($"Speaker[{code}]");
                header.Add($"Text[{code}]");
            }

            rows.Add(header);

            int localeCount = locales.Count;

            // Start node first
            EmitNode(so.EditorStartNode, so, locales, collections, linkMap, rows, localeCount);

            foreach (NodeData node in so.EditorNodes) EmitNode(node, so, locales, collections, linkMap, rows, localeCount);

            return CsvUtility.ToCsv(rows);
        }

        private static void EmitNode(NodeData node, DialogueSO so, IReadOnlyList<Locale> locales,
            IReadOnlyList<LocalizationTableCollection> collections, Dictionary<(string, int), string> linkMap,
            List<List<string>> rows, int localeCount)
        {
            string px = node.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string py = node.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture);

            switch (node.type)
            {
                case NodeType.Start:
                    rows.Add(MakeRow(node.guid, "Start", Next(linkMap, node.guid, 0), "", "", "", "", "", "", px, py, localeCount));
                    break;

                case NodeType.Dialogue:
                    {
                        IReadOnlyList<DataWrapper> opt = node.OptionDatas;

                        string speakerTable = opt[0].data[0].ToString();
                        string speakerKey = opt[1].data[0].ToString();
                        string textTable = opt[2].data[0].ToString();
                        string textKey = opt[3].data[0].ToString();
                        string message = opt[4].data[0].ToString();

                        List<string> row = MakeRow(node.guid, "Dialogue", Next(linkMap, node.guid, 0), message, "", speakerTable, textTable, speakerKey, textKey, px, py, localeCount);

                        FillLocaleColumns(row, locales, collections, speakerTable, speakerKey, textTable, textKey);

                        rows.Add(row);
                    }
                    break;

                case NodeType.Choice:
                    {
                        IReadOnlyList<DataWrapper> opt = node.OptionDatas;

                        string speakerTable = opt[0].data[0].ToString();
                        string speakerKey = opt[1].data[0].ToString();
                        string textTable = opt[2].data[0].ToString();
                        string message = opt[3].data[0].ToString();

                        List<string> row = MakeRow(node.guid, "Choice", "", message, "", speakerTable, textTable, speakerKey, "", px, py, localeCount);

                        FillLocaleColumns(row, locales, collections, speakerTable, speakerKey, "", "");

                        rows.Add(row);

                        IReadOnlyList<DataWrapper> ports = node.PortDatas;

                        for (int i = 0; i < ports.Count; i++)
                        {
                            IReadOnlyList<GenericData> data = ports[i].data;

                            string optionKey = data[0].ToString();
                            bool hide = data[2].GetBool();

                            string conditions = ConditionsToString(data, 3);
                            string optionData = hide ? (conditions.Length == 0 ? "hide" : "hide; " + conditions) : conditions;

                            List<string> optionRow = MakeRow("", "Option", Next(linkMap, node.guid, i), "", optionData, "", "", "", optionKey, "", "", localeCount);

                            FillLocaleColumns(optionRow, locales, collections, "", "", textTable, optionKey);

                            rows.Add(optionRow);
                        }
                    }
                    break;

                case NodeType.Trigger:
                    rows.Add(MakeRow(node.guid, "Trigger", Next(linkMap, node.guid, 0), "", TriggerToString(node.OptionDatas), "", "", "", "", px, py, localeCount));
                    break;

                case NodeType.Event:
                    rows.Add(MakeRow(node.guid, "Event", Next(linkMap, node.guid, 0), "", EventToString(node.OptionDatas), "", "", "", "", px, py, localeCount));
                    break;

                case NodeType.Wait:
                    rows.Add(MakeRow(node.guid, "Wait", Next(linkMap, node.guid, 0), "", WaitToString(node.OptionDatas), "", "", "", "", px, py, localeCount));
                    break;

                case NodeType.Object:
                    {
                        // Object references can't be represented in CSV; keep the node's id, link and
                        // position so topology survives a round-trip. Each referenced object becomes an
                        // "Asset" sub-row showing its name (reference-only — re-import reuses editor slots).
                        rows.Add(MakeRow(node.guid, "Object", Next(linkMap, node.guid, 0), "", "", "", "", "", "", px, py, localeCount));

                        IReadOnlyList<DataWrapper> objs = node.OptionDatas;

                        for (int i = 0; i < objs.Count; i++)
                        {
                            IReadOnlyList<GenericData> data = objs[i].data;

                            string name = "";

                            if (data.Count > 0 && data[0].TryGetObject(out UnityEngine.Object obj) && obj != null) name = obj.name;

                            rows.Add(MakeRow("", "Asset", "", name, "", "", "", "", "", "", "", localeCount));
                        }
                    }
                    break;

                case NodeType.Condition:
                    {
                        // Port 0 = default/else branch -> node row's Next
                        rows.Add(MakeRow(node.guid, "Condition", Next(linkMap, node.guid, 0), "", "", "", "", "", "", px, py, localeCount));

                        IReadOnlyList<DataWrapper> ports = node.PortDatas;

                        for (int i = 1; i < ports.Count; i++)
                        {
                            string conditions = ConditionsToString(ports[i].data, 0);

                            rows.Add(MakeRow("", "Branch", Next(linkMap, node.guid, i), "", conditions, "", "", "", "", "", "", localeCount));
                        }
                    }
                    break;
            }
        }

        private static List<string> MakeRow(string id, string type, string next, string message, string data,
            string speakerTable, string textTable, string speakerKey, string textKey, string x, string y, int localeCount)
        {
            List<string> row = new() { id, type, next, message, data, speakerTable, textTable, speakerKey, textKey, x, y };

            for (int i = 0; i < localeCount; i++)
            {
                row.Add("");
                row.Add("");
            }

            return row;
        }

        private static void FillLocaleColumns(List<string> row, IReadOnlyList<Locale> locales,
            IReadOnlyList<LocalizationTableCollection> collections,
            string speakerTable, string speakerKey, string textTable, string textKey)
        {
            for (int i = 0; i < locales.Count; i++)
            {
                LocaleIdentifier id = locales[i].Identifier;

                int speakerColumn = 11 + i * 2;
                int textColumn = speakerColumn + 1;

                row[speakerColumn] = ResolveValue(collections, speakerTable, speakerKey, id);
                row[textColumn] = ResolveValue(collections, textTable, textKey, id);
            }
        }

        private static string ResolveValue(IReadOnlyList<LocalizationTableCollection> collections,
            string tableName, string key, LocaleIdentifier locale)
        {
            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key)) return "";

            for (int i = 0; i < collections.Count; i++)
            {
                if (collections[i].TableCollectionName == tableName)
                    return new EntryData(0, key, collections[i].Tables).GetText(locale);
            }

            return "";
        }

        private static string Next(Dictionary<(string, int), string> linkMap, string guid, int port)
            => linkMap.TryGetValue((guid, port), out string target) ? target : "";

        private static string TriggerToString(IReadOnlyList<DataWrapper> optionDatas)
        {
            List<string> parts = new();

            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> data = optionDatas[i].data;

                string key = data[0].ToString();

                if (string.IsNullOrEmpty(key)) continue;

                switch (data[1].Type)
                {
                    case GenericData.DataType.Float:
                        {
                            string op = data[2].GetEnum<ValueSetterType>() switch
                            {
                                ValueSetterType.Plus => "+",
                                ValueSetterType.Minus => "-",
                                _ => "=",
                            };

                            parts.Add($"{key}{op}{data[1].GetFloat()}");
                        }
                        break;

                    case GenericData.DataType.Bool:
                        parts.Add($"{key}={(data[1].GetBool() ? "true" : "false")}");
                        break;
                }
            }

            return string.Join("; ", parts);
        }

        private static string EventToString(IReadOnlyList<DataWrapper> optionDatas)
        {
            List<string> parts = new();

            for (int i = 0; i < optionDatas.Count; i++)
            {
                string value = optionDatas[i].data[0].ToString();

                if (!string.IsNullOrEmpty(value)) parts.Add(value);
            }

            return string.Join("; ", parts);
        }

        private static string WaitToString(IReadOnlyList<DataWrapper> optionDatas)
        {
            IReadOnlyList<GenericData> data = optionDatas[0].data;

            if (data[1].TryGetFloat(out float seconds)) return seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

            string reason = data[1].ToString();

            return string.IsNullOrEmpty(reason) ? "notify" : $"notify:{reason}";
        }

        private static string ConditionsToString(IReadOnlyList<GenericData> data, int start)
        {
            List<string> parts = new();

            for (int j = start; j + 2 < data.Count; j += 3)
            {
                string key = data[j].ToString();

                if (string.IsNullOrEmpty(key)) continue;

                switch (data[j + 1].Type)
                {
                    case GenericData.DataType.Float:
                        {
                            string op = data[j + 2].GetEnum<ValueCheckType>() switch
                            {
                                ValueCheckType.Less => "<",
                                ValueCheckType.LessEqual => "<=",
                                ValueCheckType.Equal => "==",
                                ValueCheckType.NotEqual => "!=",
                                ValueCheckType.Greater => ">",
                                ValueCheckType.GreaterEqual => ">=",
                                _ => "==",
                            };

                            parts.Add($"{key}{op}{data[j + 1].GetFloat()}");
                        }
                        break;

                    case GenericData.DataType.Bool:
                        parts.Add($"{key}=={(data[j + 1].GetBool() ? "true" : "false")}");
                        break;
                }
            }

            return string.Join("; ", parts);
        }
    }
}
