using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    internal sealed class DialogueCsvChange
    {
        public string category;
        public string target;
        public string field;
        public string before;
        public string after;
        public int line;
    }

    // Read-only comparison. Nothing is changed until DialogueCsvImporter.Import is called.
    internal sealed class DialogueCsvImportPreview
    {
        private sealed class NodeSnapshot
        {
            public string id;
            public string type;
            public List<string> values;
            public readonly List<List<string>> details = new();
        }

        private readonly List<DialogueCsvChange> changes = new();
        private readonly HashSet<string> recorded = new(StringComparer.Ordinal);
        private readonly Dictionary<string, StringTableCollection> collectionByName = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, StringTableCollection> collectionByGuid = new();
        private readonly HashSet<string> tableChanges = new(StringComparer.Ordinal);
        private readonly HashSet<string> entryChanges = new(StringComparer.Ordinal);
        private readonly HashSet<string> localeChanges = new(StringComparer.Ordinal);
        private readonly HashSet<string> configuredLocales = new(StringComparer.Ordinal);
        private readonly DialogueSO asset;
        private readonly DialogueCsvImportOptions options;
        private string firstCsvNodeId;
        private Dictionary<string, int> columns;
        private readonly Dictionary<List<string>, int> sourceLines = new();

        public IReadOnlyList<DialogueCsvChange> Changes => changes;
        public int NodeChanges { get; private set; }
        public int ConnectionChanges { get; private set; }
        public int LocalizationChanges { get; private set; }

        private DialogueCsvImportPreview(DialogueSO so, DialogueCsvImportOptions importOptions)
        {
            asset = so;
            options = importOptions;
        }

        public static DialogueCsvImportPreview Build(DialogueSO so, string csv, DialogueCsvImportOptions options)
        {
            DialogueCsvImportPreview preview = new(so, options);
            preview.Compare(csv);

            return preview;
        }

        private void Compare(string csv)
        {
            List<List<string>> targetRows = CsvUtility.FromCsv(csv, out List<int> csvLines);

            if (targetRows.Count == 0) return;

            for (int i = 1; i < targetRows.Count; i++)
            {
                sourceLines[targetRows[i]] = csvLines[i];

                if (firstCsvNodeId != null || targetRows[i].Count < 2 || !DialogueCsvImporter.IsNodeType(targetRows[i][1]) || targetRows[i][1] == "Start") continue;

                firstCsvNodeId = targetRows[i][0];
            }

            columns = Headers(targetRows[0]);
            List<List<string>> currentRows = CsvUtility.FromCsv(DialogueCsvExporter.Export(asset));
            Dictionary<string, int> currentColumns = Headers(currentRows[0]);
            Dictionary<string, NodeSnapshot> current = Nodes(currentRows, currentColumns);
            Dictionary<string, NodeSnapshot> target = Nodes(targetRows, columns);

            CompareGraph(current, target, currentColumns);
            CompareLocalization(targetRows);
        }

        private void CompareGraph(Dictionary<string, NodeSnapshot> current, Dictionary<string, NodeSnapshot> target, Dictionary<string, int> oldColumns)
        {
            foreach (KeyValuePair<string, NodeSnapshot> existing in current)
            {
                if (target.ContainsKey(existing.Key)) continue;

                if (existing.Value.type == "Start" && !ContainsStart(target))
                {
                    if (options.keepStart)
                    {
                        string next = firstCsvNodeId;
                        string before = Cell(existing.Value.values, oldColumns, "Next");
                        Add("Connection", existing.Key, "Next", before, next, 0);
                        continue;
                    }

                    Add("Node", existing.Key, "Start", "Existing Start", "New Start ID (generated)", 0);
                    string first = firstCsvNodeId;

                    if (!string.IsNullOrEmpty(first)) Add("Connection", "(generated Start)", "Next", "(none)", first, 0);

                    continue;
                }

                Add("Node", existing.Key, "Remove", existing.Value.type, "(removed)", 0);
                string oldNext = Cell(existing.Value.values, oldColumns, "Next");

                if (!string.IsNullOrEmpty(oldNext)) Add("Connection", existing.Key, "Next", oldNext, "(removed)", 0);

                for (int i = 0; i < existing.Value.details.Count; i++)
                {
                    string detailNext = Cell(existing.Value.details[i], oldColumns, "Next");

                    if (!string.IsNullOrEmpty(detailNext)) Add("Connection", existing.Key + " / detail " + (i + 1), "Next", detailNext, "(removed)", 0);
                }
            }

            foreach (KeyValuePair<string, NodeSnapshot> pair in target)
            {
                NodeSnapshot desired = pair.Value;
                int line = Line(desired.values);

                if (!current.TryGetValue(pair.Key, out NodeSnapshot existing))
                {
                    Add("Node", desired.id, "Create", "(none)", desired.type, line);
                    AddNewFields(desired);
                    AddNewLinks(desired, columns);
                    continue;
                }

                if (existing.type != desired.type) Add("Node", desired.id, "Type", existing.type, desired.type, line);

                CompareField(desired.id, "Message", existing.values, desired.values, oldColumns, columns, line, "Node");
                CompareField(desired.id, "Data", existing.values, desired.values, oldColumns, columns, line, "Node");
                ComparePosition(desired.id, "X", existing.values, desired.values, oldColumns, line);
                ComparePosition(desired.id, "Y", existing.values, desired.values, oldColumns, line);
                CompareReference(desired.id, "Speaker", existing.values, desired.values, oldColumns, line);
                CompareReference(desired.id, "Text", existing.values, desired.values, oldColumns, line);

                CompareField(desired.id, "Next", existing.values, desired.values, oldColumns, columns, line, "Connection");

                int max = Math.Max(existing.details.Count, desired.details.Count);

                for (int i = 0; i < max; i++)
                {
                    string targetName = desired.id + " / detail " + (i + 1);

                    if (i >= existing.details.Count)
                    {
                        List<string> added = desired.details[i];
                        Add("Node", targetName, "Add " + Cell(added, columns, "Type"), "(none)", Detail(added, columns), Line(added));
                        string link = Cell(added, columns, "Next");

                        if (!string.IsNullOrEmpty(link)) Add("Connection", targetName, "Next", "(none)", link, Line(added));
                        continue;
                    }

                    if (i >= desired.details.Count)
                    {
                        List<string> removed = existing.details[i];
                        Add("Node", targetName, "Remove " + Cell(removed, oldColumns, "Type"), Detail(removed, oldColumns), "(removed)", line);
                        string link = Cell(removed, oldColumns, "Next");

                        if (!string.IsNullOrEmpty(link)) Add("Connection", targetName, "Next", link, "(none)", line);
                        continue;
                    }

                    List<string> before = existing.details[i];
                    List<string> after = desired.details[i];
                    string beforeType = Cell(before, oldColumns, "Type");
                    string afterType = Cell(after, columns, "Type");

                    if (beforeType != afterType) Add("Node", targetName, "Detail type", beforeType, afterType, Line(after));

                    CompareField(targetName, "Message", before, after, oldColumns, columns, Line(after), "Node");
                    CompareField(targetName, "Data", before, after, oldColumns, columns, Line(after), "Node");

                    CompareField(targetName, "Next", before, after, oldColumns, columns, Line(after), "Connection");
                }
            }
        }

        private void AddNewFields(NodeSnapshot node)
        {
            int line = Line(node.values);
            string[] fields = { "Message", "Data", "X", "Y", "SpeakerTableGuid", "TextTableGuid", "SpeakerEntryId", "TextEntryId" };

            foreach (string field in fields)
            {
                string value = Cell(node.values, columns, field);

                if (string.IsNullOrEmpty(value))
                {
                    if (field != "X" && field != "Y") continue;

                    value = "(automatic placement)";
                }

                Add("Node", node.id, field, "(none)", value, line);
            }

            for (int i = 0; i < node.details.Count; i++)
            {
                List<string> detail = node.details[i];
                Add("Node", node.id + " / detail " + (i + 1), "Create " + Cell(detail, columns, "Type"), "(none)", Detail(detail, columns), Line(detail));
            }
        }

        private void AddNewLinks(NodeSnapshot node, Dictionary<string, int> fields)
        {
            int line = Line(node.values);

            string next = Cell(node.values, fields, "Next");

            if (node.type != "Choice" && !string.IsNullOrEmpty(next)) Add("Connection", node.id, "Next", "(none)", next, line);

            for (int i = 0; i < node.details.Count; i++)
            {
                List<string> detail = node.details[i];
                string type = Cell(detail, fields, "Type");

                string detailNext = Cell(detail, fields, "Next");

                if ((type == "Option" || type == "Branch") && !string.IsNullOrEmpty(detailNext)) Add("Connection", node.id + " / " + type + " " + (i + 1), "Next", "(none)", detailNext, Line(detail));
            }
        }

        private void CompareReference(string id, string kind, List<string> before, List<string> after, Dictionary<string, int> oldColumns, int line)
        {
            string oldTable = Cell(before, oldColumns, kind + "TableGuid");
            string newTable = Cell(after, columns, kind + "TableGuid");

            if (string.IsNullOrEmpty(oldTable)) oldTable = Cell(before, oldColumns, kind + "Table");

            if (string.IsNullOrEmpty(newTable)) newTable = Cell(after, columns, kind + "Table");

            // Names are not graph reference changes when the table GUID stays the same.
            if (!SameIdentity(oldTable, newTable)) Add("Node", id, kind + " table reference", oldTable, newTable, line);

            string oldEntry = Cell(before, oldColumns, kind + "EntryId");
            string newEntry = Cell(after, columns, kind + "EntryId");

            if (string.IsNullOrEmpty(oldEntry)) oldEntry = Cell(before, oldColumns, kind + "Key");

            if (string.IsNullOrEmpty(newEntry)) newEntry = Cell(after, columns, kind + "Key");

            if (oldEntry != newEntry) Add("Node", id, kind + " entry reference", oldEntry, newEntry, line);
        }

        private void ComparePosition(string id, string field, List<string> before, List<string> after, Dictionary<string, int> oldColumns, int line)
        {
            string previous = Cell(before, oldColumns, field);
            string next = Cell(after, columns, field);
            bool previousValid = float.TryParse(previous, NumberStyles.Float, CultureInfo.InvariantCulture, out float previousFloat);
            bool nextValid = float.TryParse(next, NumberStyles.Float, CultureInfo.InvariantCulture, out float nextFloat);

            if (previousValid && nextValid && Mathf.Approximately(previousFloat, nextFloat)) return;

            if (!nextValid) next = "(automatic placement)";

            Add("Node", id, "Position " + field, previous, next, line);
        }

        private void CompareField(string id, string field, List<string> before, List<string> after, Dictionary<string, int> oldColumns, Dictionary<string, int> newColumns, int line, string category)
        {
            Add(category, id, field, Cell(before, oldColumns, field), Cell(after, newColumns, field), line);
        }

        private void CompareLocalization(List<List<string>> rows)
        {
            foreach (StringTableCollection collection in LocalizationEditorSettings.GetStringTableCollections())
            {
                collectionByName[collection.TableCollectionName] = collection;
                collectionByGuid[collection.TableCollectionNameReference.TableCollectionNameGuid] = collection;
            }

            foreach (Locale locale in LocalizationEditorSettings.GetLocales()) configuredLocales.Add(locale.Identifier.Code);

            int ownerRow = -1;

            for (int row = 1; row < rows.Count; row++)
            {
                List<string> values = rows[row];
                string type = Cell(values, columns, "Type");

                if (DialogueCsvImporter.IsNodeType(type)) ownerRow = row;

                if (type == "Dialogue" || type == "Choice") CompareEntry(rows, row, row, true);

                if (type == "Dialogue" || type == "Option") CompareEntry(rows, ownerRow, row, false);
            }
        }

        private void CompareEntry(List<List<string>> rows, int owner, int row, bool speaker)
        {
            if (owner < 1) return;

            string kind = speaker ? "Speaker" : "Text";
            List<string> values = rows[row];
            List<string> parent = rows[owner];
            string tableGuid = Cell(parent, columns, kind + "TableGuid");
            string tableName = Cell(parent, columns, kind + "Table");
            string key = Cell(values, columns, kind + "Key");
            string idString = Cell(values, columns, kind + "EntryId");
            long.TryParse(idString, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id);

            if (id == 0 && string.IsNullOrEmpty(key)) return;

            StringTableCollection collection = null;
            Guid guid = Guid.Empty;

            if (Guid.TryParse(tableGuid, out guid)) collectionByGuid.TryGetValue(guid, out collection);
            else if (string.IsNullOrEmpty(tableGuid) && !string.IsNullOrEmpty(tableName)) collectionByName.TryGetValue(tableName, out collection);

            if (collection != null)
            {
                string canonical = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString("N");

                if (!string.IsNullOrEmpty(tableName) && collection.TableCollectionName != tableName && tableChanges.Add(canonical))
                    Add("Localization", canonical, "Rename table", collection.TableCollectionName, tableName, Line(parent));
            }
            else if (!string.IsNullOrEmpty(tableName) && options.createTables && tableChanges.Add("new:" + tableName))
                Add("Localization", tableName, "Create table", "(none)", tableName, Line(parent));

            string identity = collection != null ? collection.TableCollectionNameReference.TableCollectionNameGuid.ToString("N") : !string.IsNullOrEmpty(tableGuid) ? tableGuid : tableName;
            string entryIdentity = identity + ":" + (id > 0 ? id.ToString(CultureInfo.InvariantCulture) : "key:" + key);
            SharedTableData.SharedTableEntry currentEntry = collection == null ? null : id > 0 ? collection.SharedData.GetEntry(id) : collection.SharedData.GetEntry(key);

            if (currentEntry == null)
            {
                if (options.createEntries && entryChanges.Add(entryIdentity)) Add("Localization", entryIdentity, "Create entry", "(none)", key, Line(values));
            }
            else if (entryChanges.Add(entryIdentity) && currentEntry.Key != key)
                Add("Localization", entryIdentity, "Rename key", currentEntry.Key, key, Line(values));

            foreach (KeyValuePair<string, int> field in columns)
            {
                if (!field.Key.StartsWith(kind + "[", StringComparison.Ordinal) || !field.Key.EndsWith("]", StringComparison.Ordinal)) continue;

                string locale = field.Key.Substring(kind.Length + 1, field.Key.Length - kind.Length - 2);

                if (!configuredLocales.Contains(locale)) continue;

                string after = Cell(values, field.Value);
                string before = "(no entry)";
                StringTable localeTable = null;

                if (collection != null)
                {
                    foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
                    {
                        if (reference.asset is not StringTable table || table.LocaleIdentifier.Code != locale) continue;

                        localeTable = table;
                        break;
                    }
                }

                StringTableEntry existing = currentEntry == null ? null : localeTable?.GetEntry(currentEntry.Id);

                // An empty CSV cell clears an existing translation, but does not create a missing one.
                if (existing == null && string.IsNullOrEmpty(after)) continue;

                if (localeTable == null && collection != null)
                {
                    if (options.createLocaleTables && localeChanges.Add(identity + ":" + locale))
                        Add("Localization", identity, "Create locale table " + locale, "(none)", locale, Line(values));

                    if (!options.createLocaleTables) continue;
                }

                if (existing != null) before = existing.Value;

                if (currentEntry == null && !options.createEntries) continue;

                string translationIdentity = entryIdentity + ":" + locale;

                if (localeChanges.Add(translationIdentity)) Add("Localization", entryIdentity, locale + " translation", before, after, Line(values));
            }
        }

        private static Dictionary<string, NodeSnapshot> Nodes(List<List<string>> rows, Dictionary<string, int> fields)
        {
            Dictionary<string, NodeSnapshot> nodes = new(StringComparer.Ordinal);
            NodeSnapshot parent = null;

            for (int i = 1; i < rows.Count; i++)
            {
                List<string> row = rows[i];
                string type = Cell(row, fields, "Type");

                if (DialogueCsvImporter.IsNodeType(type))
                {
                    string id = Cell(row, fields, "ID").Trim();

                    if (string.IsNullOrEmpty(id) || nodes.ContainsKey(id)) continue;

                    parent = new NodeSnapshot { id = id, type = type, values = row };
                    nodes.Add(id, parent);
                }
                else if (DialogueCsvImporter.IsDetailType(type) && parent != null) parent.details.Add(row);
            }

            return nodes;
        }

        private static bool ContainsStart(Dictionary<string, NodeSnapshot> nodes)
        {
            foreach (NodeSnapshot node in nodes.Values)
            {
                if (node.type == "Start") return true;
            }

            return false;
        }

        private static string Detail(List<string> values, Dictionary<string, int> fields)
            => Cell(values, fields, "Message") + " " + Cell(values, fields, "Data");

        private int Line(List<string> values)
        {
            return sourceLines.TryGetValue(values, out int line) ? line : 0;
        }

        private void Add(string category, string target, string field, string before, string after, int line)
        {
            before ??= "";
            after ??= "";

            if (before == after) return;

            string identity = category + "\u001f" + target + "\u001f" + field;

            if (!recorded.Add(identity)) return;

            changes.Add(new DialogueCsvChange { category = category, target = target, field = field, before = before, after = after, line = line });

            switch (category)
            {
                case "Node": NodeChanges++; break;
                case "Connection": ConnectionChanges++; break;
                case "Localization": LocalizationChanges++; break;
            }
        }

        private static bool SameIdentity(string first, string second)
        {
            if (Guid.TryParse(first, out Guid a) && Guid.TryParse(second, out Guid b)) return a == b;

            return first == second;
        }

        private static Dictionary<string, int> Headers(List<string> header)
        {
            Dictionary<string, int> result = new(StringComparer.Ordinal);

            for (int i = 0; i < header.Count; i++)
            {
                if (!result.ContainsKey(header[i])) result.Add(header[i], i);
            }

            return result;
        }

        private static int Index(Dictionary<string, int> fields, string name) => fields.TryGetValue(name, out int column) ? column : -1;
        private static string Cell(List<string> row, Dictionary<string, int> fields, string name) => Cell(row, Index(fields, name));
        private static string Cell(List<string> row, int column) => column < 0 || column >= row.Count ? "" : row[column];
    }

    internal sealed class DialogueCsvImportPreviewWindow : EditorWindow
    {
        private readonly List<DialogueCsvChange> visible = new();
        private DialogueCsvImportPreview preview;
        private bool canImport;
        private bool ignoredErrors;
        private string contextMessage;
        private string search = "";
        private string category = "All";
        private ListView list;
        private VisualElement details;
        private bool accepted;

        public static bool Display(DialogueCsvImportPreview changes, bool allowImport, bool ignored, string message = "")
        {
            DialogueCsvImportPreviewWindow window = CreateInstance<DialogueCsvImportPreviewWindow>();
            window.preview = changes;
            window.canImport = allowImport;
            window.ignoredErrors = ignored;
            window.contextMessage = message;
            window.titleContent = new GUIContent(allowImport ? "Confirm Dialogue Import" : "Dialogue Import Changes");
            window.minSize = new Vector2(860, 580);
            window.position = new Rect(150, 120, 1000, 680);
            window.ShowModalUtility();
            bool result = window.accepted;

            return result;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.flexDirection = FlexDirection.Column;
            Label title = new(canImport ? "Review final CSV import changes" : "Preview CSV import changes");
            title.style.fontSize = 17;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);
            Label description = new($"{preview.Changes.Count} changes · {preview.NodeChanges} node fields · {preview.ConnectionChanges} connections · {preview.LocalizationChanges} Localization changes");
            description.style.marginTop = 7;
            description.style.marginBottom = 10;
            root.Add(description);

            if (!canImport) root.Add(new HelpBox("This is a provisional preview. Resolve outstanding CSV decisions to see the final changes.", HelpBoxMessageType.Warning));

            if (ignoredErrors) root.Add(new HelpBox("Preview is based on the sanitized CSV. Invalid rows and values will be skipped; the original CSV will not be modified.", HelpBoxMessageType.Warning));

            if (!string.IsNullOrEmpty(contextMessage))
            {
                HelpBox notices = new(contextMessage, HelpBoxMessageType.Info);
                root.Add(notices);
            }

            VisualElement filters = new() { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            TextField searchInput = new("Search changes") { value = search };
            searchInput.style.flexGrow = 1;
            searchInput.RegisterValueChangedCallback(evt => { search = evt.newValue; Refresh(); });
            filters.Add(searchInput);
            DropdownField kind = new("Category", new List<string> { "All", "Node", "Connection", "Localization" }, 0);
            kind.style.minWidth = 210;
            kind.RegisterValueChangedCallback(evt => { category = evt.newValue; Refresh(); });
            filters.Add(kind);
            root.Add(filters);

            TwoPaneSplitView panes = new(0, 525, TwoPaneSplitViewOrientation.Horizontal);
            panes.style.flexGrow = 1;
            list = new ListView { itemsSource = visible, fixedItemHeight = 65, showBorder = true, selectionType = SelectionType.Single };
            list.makeItem = () =>
            {
                VisualElement item = new();
                item.style.paddingLeft = 7;
                item.style.paddingRight = 7;
                item.style.paddingTop = 6;
                Label heading = new() { name = "heading" };
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                item.Add(heading);
                Label summary = new() { name = "summary" };
                summary.style.fontSize = 11;
                summary.style.overflow = Overflow.Hidden;
                item.Add(summary);

                return item;
            };
            list.bindItem = (item, index) =>
            {
                DialogueCsvChange change = visible[index];
                item.Q<Label>("heading").text = $"{change.category} · {change.target} · {change.field}";
                item.Q<Label>("summary").text = Short(change.before) + "  →  " + Short(change.after);
                item.tooltip = change.before + "\n→\n" + change.after;
            };
            list.selectionChanged += selection =>
            {
                foreach (object item in selection)
                {
                    ShowDetail((DialogueCsvChange)item);
                    break;
                }
            };
            panes.Add(list);
            ScrollView right = new();
            right.style.paddingLeft = 12;
            right.style.paddingRight = 6;
            details = new VisualElement();
            right.Add(details);
            panes.Add(right);
            root.Add(panes);

            VisualElement buttons = new() { style = { flexDirection = FlexDirection.Row, marginTop = 9 } };
            buttons.Add(new Label("No assets are changed until you confirm the import.") { style = { flexGrow = 1 } });
            buttons.Add(new Button(Close) { text = canImport ? "Back" : "Close" });

            if (canImport)
            {
                Button confirm = new(() => { accepted = true; Close(); }) { text = ignoredErrors ? "Import sanitized CSV" : "Apply these changes" };
                confirm.style.marginLeft = 8;
                buttons.Add(confirm);
            }

            root.Add(buttons);
            Refresh();
        }

        private void Refresh()
        {
            visible.Clear();

            foreach (DialogueCsvChange change in preview.Changes)
            {
                if (category != "All" && change.category != category) continue;

                if (!string.IsNullOrWhiteSpace(search) && (change.target + " " + change.field + " " + change.before + " " + change.after).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                visible.Add(change);
            }

            list?.RefreshItems();
            details?.Clear();

            if (visible.Count > 0)
            {
                list?.SetSelection(0);
                ShowDetail(visible[0]);
            }
            else details?.Add(new Label(preview.Changes.Count == 0 ? "No value differences detected. The graph is still rebuilt on import." : "No matching changes."));
        }

        private void ShowDetail(DialogueCsvChange change)
        {
            if (details == null) return;

            details.Clear();
            Label title = new($"{change.category} · {change.field}");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            details.Add(title);
            details.Add(new Label(change.target));

            if (change.line > 0) details.Add(new Label("CSV line " + change.line));

            TextField previous = new("Current value") { value = change.before, multiline = true, isReadOnly = true };
            previous.style.marginTop = 14;
            details.Add(previous);
            TextField next = new("CSV value") { value = change.after, multiline = true, isReadOnly = true };
            next.style.marginTop = 12;
            details.Add(next);
        }

        private static string Short(string value)
        {
            string text = string.IsNullOrEmpty(value) ? "(empty)" : value.Replace('\r', ' ').Replace('\n', ' ');

            return text.Length <= 64 ? text : text.Substring(0, 64) + "...";
        }
    }
}
