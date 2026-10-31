using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Yang.Dialogue.Editor
{
    public static class DialogueCsvExporter
    {
        public static string Export(DialogueSO so)
        {
            IReadOnlyList<Locale> locales = LocalizationEditorSettings.GetLocales();
            IReadOnlyList<LocalizationTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
            Dictionary<(string, int), string> linkMap = new();
            foreach (LinkData link in so.EditorLinks)
                linkMap[(link.nodeGuid, link.outPortIndex)] = link.targetGuid;
            List<List<string>> rows = new();
            List<string> header = new() { "ID", "Type", "Next", "Message", "Data", "SpeakerTable", "TextTable", "SpeakerKey", "TextKey", "X", "Y" };

            foreach (Locale locale in locales)
            {
                string code = locale.Identifier.Code;
                header.Add($"Speaker[{code}]");
                header.Add($"Text[{code}]");
            }

            header.AddRange(new[] { "SpeakerTableGuid", "TextTableGuid", "SpeakerEntryId", "TextEntryId" });

            foreach (Locale locale in locales)
            {
                header.Add($"SpeakerPresent[{locale.Identifier.Code}]");
                header.Add($"TextPresent[{locale.Identifier.Code}]");
            }

            rows.Add(header);
            int localeCount = locales.Count;
            EmitNode(so.EditorStartNode, so, locales, collections, linkMap, rows, localeCount);
            foreach (NodeData node in so.EditorNodes)
                EmitNode(node, so, locales, collections, linkMap, rows, localeCount);
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
                        (string name, string key, System.Guid guid, long id) speaker = GetReference(collections, opt[0].data, opt[1].data);
                        (string name, string key, System.Guid guid, long id) text = GetReference(collections, opt[2].data, opt[3].data);
                        string message = opt[4].data[0].ToString();
                        List<string> row = MakeRow(node.guid, "Dialogue", Next(linkMap, node.guid, 0),
                            message, "", speaker.name, text.name, speaker.key, text.key, px, py, localeCount);
                        FillLocaleColumns(row, locales, collections, speaker, text);
                        SetIdentity(row, localeCount, speaker, text);
                        rows.Add(row);
                    }
                    break;
                case NodeType.Choice:
                    {
                        IReadOnlyList<DataWrapper> opt = node.OptionDatas;
                        (string name, string key, System.Guid guid, long id) speaker = GetReference(collections, opt[0].data, opt[1].data);
                        (string name, string key, System.Guid guid, long id) text = GetReference(collections, opt[2].data);
                        string message = opt[3].data[0].ToString();
                        List<string> row = MakeRow(node.guid, "Choice", "", message, "", speaker.name, text.name, speaker.key, "", px, py, localeCount);
                        FillLocaleColumns(row, locales, collections, speaker, default);
                        SetIdentity(row, localeCount, speaker, text);
                        rows.Add(row);
                        IReadOnlyList<DataWrapper> ports = node.PortDatas;

                        for (int i = 0; i < ports.Count; i++)
                        {
                            IReadOnlyList<GenericData> data = ports[i].data;
                            (string name, string key, System.Guid guid, long id) option = GetReference(collections, opt[2].data, data);
                            bool hide = data[2].GetBool();
                            string conditions = ConditionsToString(data, 3);
                            string optionData = hide ? (conditions.Length == 0 ? "hide" : "hide; " + conditions) : conditions;
                            List<string> optionRow = MakeRow("", "Option", Next(linkMap, node.guid, i), "", optionData, "", "", "", option.key, "", "", localeCount);
                            FillLocaleColumns(optionRow, locales, collections, default, option);
                            SetIdentity(optionRow, localeCount, default, option);
                            rows.Add(optionRow);
                        }
                    }
                    break;
                case NodeType.Trigger:
                    rows.Add(MakeRow(node.guid, "Trigger", Next(linkMap, node.guid, 0), "", TriggerToString(node.OptionDatas), "", "", "", "", px, py, localeCount));
                    break;
                case NodeType.Event:
                    {
                        rows.Add(MakeRow(node.guid, "Event", Next(linkMap, node.guid, 0), "", "", "", "", "", "", px, py, localeCount));
                        IReadOnlyList<DataWrapper> events = node.OptionDatas;

                        for (int i = 0; i < events.Count; i++)
                        {
                            IReadOnlyList<GenericData> data = events[i].data;
                            string id = data.Count > 0 ? data[0].ToString() : "";
                            string arguments = CommandNode.ArgumentsToString(data, true);
                            rows.Add(MakeRow("", "Instruction", "", id, arguments, "", "", "", "", "", "", localeCount));
                        }
                    }
                    break;
                case NodeType.Wait:
                    rows.Add(MakeRow(node.guid, "Wait", Next(linkMap, node.guid, 0), "", WaitToString(node.OptionDatas), "", "", "", "", px, py, localeCount));
                    break;
                case NodeType.Command:
                    {
                        rows.Add(MakeRow(node.guid, "Command", Next(linkMap, node.guid, 0), "", "", "", "", "", "", px, py, localeCount));
                        IReadOnlyList<DataWrapper> commands = node.OptionDatas;

                        for (int i = 0; i < commands.Count; i++)
                        {
                            IReadOnlyList<GenericData> data = commands[i].data;
                            string id = data.Count > 0 ? data[0].ToString() : "";
                            string arguments = CommandNode.ArgumentsToString(data, true);
                            rows.Add(MakeRow("", "Instruction", "", id, arguments, "", "", "", "", "", "", localeCount));
                        }
                    }
                    break;
                case NodeType.Condition:
                    {
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

            row.AddRange(new[] { "", "", "", "" });
            for (int i = 0; i < localeCount * 2; i++)
                row.Add("");
            return row;
        }

        private static (string name, string key, System.Guid guid, long id) GetReference(IReadOnlyList<LocalizationTableCollection> collections,
            IReadOnlyList<GenericData> table, IReadOnlyList<GenericData> entry = null)
        {
            string name = table[0].ToString();
            System.Guid guid = table[1].GetGuid();
            string key = entry == null ? "" : entry[0].ToString();
            long id = entry == null ? 0 : entry[1].GetLong();
            LocalizationTableCollection collection = collections.ResolveCollection(name, guid, id, key);

            if (collection != null)
            {
                name = collection.TableCollectionName;
                guid = collection.TableCollectionNameReference.TableCollectionNameGuid;
                SharedTableData.SharedTableEntry entryData = collection.ResolveEntry(id, key);

                if (entryData != null)
                {
                    id = entryData.Id;
                    key = entryData.Key;
                }
            }

            return (name, key, guid, id);
        }

        private static void SetIdentity(List<string> row, int localeCount, (string name, string key, System.Guid guid, long id) speaker,
            (string name, string key, System.Guid guid, long id) text)
        {
            int start = 11 + localeCount * 2;
            row[start] = speaker.guid == System.Guid.Empty ? "" : speaker.guid.ToString("N");
            row[start + 1] = text.guid == System.Guid.Empty ? "" : text.guid.ToString("N");
            row[start + 2] = speaker.id == 0 ? "" : speaker.id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            row[start + 3] = text.id == 0 ? "" : text.id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void FillLocaleColumns(List<string> row, IReadOnlyList<Locale> locales,
            IReadOnlyList<LocalizationTableCollection> collections,
            (string name, string key, System.Guid guid, long id) speaker,
            (string name, string key, System.Guid guid, long id) text)
        {
            LocalizationTableCollection speakerCollection = collections.ResolveCollection(speaker.name, speaker.guid);
            LocalizationTableCollection textCollection = collections.ResolveCollection(text.name, text.guid);

            for (int i = 0; i < locales.Count; i++)
            {
                LocaleIdentifier locale = locales[i].Identifier;
                int column = 11 + i * 2;
                StringTableEntry speakerEntry = FindLocalizedEntry(speakerCollection, locale, speaker.id, speaker.key);
                StringTableEntry textEntry = FindLocalizedEntry(textCollection, locale, text.id, text.key);
                row[column] = speakerEntry?.Value ?? "";
                row[column + 1] = textEntry?.Value ?? "";
                int presenceColumn = 11 + locales.Count * 2 + 4 + i * 2;
                row[presenceColumn] = speakerEntry == null ? "0" : "1";
                row[presenceColumn + 1] = textEntry == null ? "0" : "1";
            }
        }

        private static StringTableEntry FindLocalizedEntry(LocalizationTableCollection collection, LocaleIdentifier locale, long id, string key)
        {
            if (collection == null || (id == 0 && string.IsNullOrEmpty(key))) return null;

            foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
            {
                if (reference.asset is not StringTable table || table.LocaleIdentifier != locale) continue;
                return id != 0 ? table.GetEntry(id) : table.GetEntry(key);
            }

            return null;
        }

        private static string Next(Dictionary<(string, int), string> linkMap, string guid, int port)
            => linkMap.TryGetValue((guid, port), out string target) ? target : "";

        private static string TriggerToString(IReadOnlyList<DataWrapper> optionDatas)
        {
            List<string> parts = new();

            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> data = optionDatas[i].data;
                int fieldId = data[0].GetInt();
                if (fieldId <= 0) continue;

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
                            parts.Add($"{fieldId}{op}{data[1].GetFloat().ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                        }
                        break;
                    case GenericData.DataType.Bool:
                        parts.Add($"{fieldId}={(data[1].GetBool() ? "true" : "false")}");
                        break;
                }
            }

            return string.Join("; ", parts);
        }

        private static string WaitToString(IReadOnlyList<DataWrapper> optionDatas)
        {
            IReadOnlyList<GenericData> data = optionDatas[0].data;
            if (data[1].TryGetFloat(out float seconds))
                return seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string reason = data[1].ToString();
            return string.IsNullOrEmpty(reason) ? "notify" : $"notify:{reason}";
        }

        private static string ConditionsToString(IReadOnlyList<GenericData> data, int start)
        {
            List<string> parts = new();

            for (int j = start; j + 2 < data.Count; j += 3)
            {
                int fieldId = data[j].GetInt();
                if (fieldId <= 0) continue;

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
                            parts.Add($"{fieldId}{op}{data[j + 1].GetFloat().ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                        }
                        break;
                    case GenericData.DataType.Bool:
                        parts.Add($"{fieldId}=={(data[j + 1].GetBool() ? "true" : "false")}");
                        break;
                }
            }

            return string.Join("; ", parts);
        }
    }
}
