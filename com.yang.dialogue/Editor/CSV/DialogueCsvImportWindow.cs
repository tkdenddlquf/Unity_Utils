using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    internal sealed class DialogueCsvImportWindow : EditorWindow
    {
        private DialogueSO asset;
        private DialogueCsvImportReview review;
        private string sourcePath;
        private int selectedIssue;
        private VisualElement detailPanel;
        private ListView issueList;
        private Label statusLabel;
        private Button importButton;
        private string resultMessage;

        public static void Open(DialogueSO so, string path, string csv)
        {
            DialogueCsvImportWindow window = CreateInstance<DialogueCsvImportWindow>();
            window.asset = so;
            window.sourcePath = path;
            window.review = new DialogueCsvImportReview(so, csv);
            window.titleContent = new GUIContent("Dialogue CSV Import");
            window.minSize = new Vector2(820, 610);
            window.Show();
        }

        public void CreateGUI()
        {
            if (review == null)
            {
                rootVisualElement.Add(new HelpBox("Reopen Import CSV from the Dialogue asset inspector.", HelpBoxMessageType.Info));
                return;
            }

            BuildWindow();
        }

        private void BuildWindow()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            Label title = new("Dialogue CSV Import");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);
            root.Add(new Label($"{asset.name}  ·  {Path.GetFileName(sourcePath)}"));
            root.Add(Spacer(8));

            VisualElement statistics = new();
            statistics.style.flexDirection = FlexDirection.Row;
            statistics.style.marginBottom = 7;
            statistics.Add(Statistic($"{review.errorCount} errors"));
            statistics.Add(Statistic($"{review.warningCount} notices"));
            statistics.Add(Statistic($"{review.nodeCount} nodes"));
            statistics.Add(Statistic($"{review.detailCount} detail rows"));
            root.Add(statistics);

            VisualElement settings = new();
            settings.style.flexDirection = FlexDirection.Row;
            settings.style.flexWrap = Wrap.Wrap;
            settings.style.marginBottom = 6;
            AddToggle(settings, "Create missing tables", review.options.createTables, value => review.options.createTables = value);
            AddToggle(settings, "Create missing entries", review.options.createEntries, value => review.options.createEntries = value);
            AddToggle(settings, "Use asset default tables", review.options.useDefaultTables, value => review.options.useDefaultTables = value);
            AddToggle(settings, "Keep existing Start if missing", review.options.keepStart, value => review.options.keepStart = value);
            root.Add(settings);

            if (review.options.createTables)
            {
                VisualElement folderRow = new();
                folderRow.style.flexDirection = FlexDirection.Row;
                TextField folder = new("New table folder") { value = review.options.tableFolder };
                folder.style.flexGrow = 1;
                folder.isDelayed = true;
                folder.RegisterValueChangedCallback(evt =>
                {
                    review.options.tableFolder = evt.newValue.Trim();
                    Rescan();
                });
                folderRow.Add(folder);
                Button browse = new(ChooseFolder) { text = "Browse..." };
                folderRow.Add(browse);
                root.Add(folderRow);
            }

            TwoPaneSplitView panes = new(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            panes.style.flexGrow = 1;
            panes.style.minHeight = 290;

            VisualElement left = new();
            left.style.flexGrow = 1;
            left.Add(SectionTitle("Problems and notices"));
            issueList = new ListView();
            issueList.itemsSource = review.issues;
            issueList.fixedItemHeight = 54;
            issueList.makeItem = () =>
            {
                Label label = new();
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.paddingTop = 5;
                label.style.paddingLeft = 7;

                return label;
            };
            issueList.bindItem = (item, index) =>
            {
                DialogueCsvIssue issue = review.issues[index];
                string location = issue.line > 0 ? $"Line {issue.line}" : "CSV";
                ((Label)item).text = $"{(issue.warning ? "INFO" : "ERROR")} · {location}\n{issue.title}";
            };
            issueList.selectionChanged += OnIssueSelected;
            issueList.style.flexGrow = 1;
            left.Add(issueList);
            panes.Add(left);

            ScrollView right = new();
            right.style.flexGrow = 1;
            right.style.paddingLeft = 12;
            right.style.paddingRight = 4;
            detailPanel = new VisualElement();
            right.Add(detailPanel);
            panes.Add(right);
            root.Add(panes);

            VisualElement footer = new();
            footer.style.marginTop = 8;
            footer.Add(SectionTitle("Changes to apply"));
            footer.Add(new Label($"Graph: {review.currentNodes} → {review.nodeCount + (HasCsvStart() ? 0 : 1)} nodes, {review.currentLinks} → up to {review.linkCount + (HasCsvStart() ? 0 : 1)} links"));
            footer.Add(new Label($"Localization: {review.newTableCount} new tables, {review.newEntryCount} new entries, {review.renameCount} key renames, {review.translationCount} translated values to write"));
            footer.Add(new Label("Import replaces the graph, updates referenced String Tables, and records Undo. No data is changed during review."));
            Foldout changes = new() { text = "See Localization changes", value = false };
            AddChangeDetails(changes, "Create tables", review.plannedTables);
            AddChangeDetails(changes, "Create entries", review.plannedEntries);
            AddChangeDetails(changes, "Rename keys", review.plannedRenames);
            AddChangeDetails(changes, "Write translations", review.plannedTranslations);
            footer.Add(changes);

            VisualElement actions = new();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 8;
            actions.Add(new Button(SaveCsv) { text = "Save corrected CSV..." });
            actions.Add(new Button(() => Close()) { text = "Cancel" });
            VisualElement push = new();
            push.style.flexGrow = 1;
            actions.Add(push);
            importButton = new Button(ApplyImport) { text = "Replace graph and import" };
            importButton.SetEnabled(review.errorCount == 0 && review.parsed && review.nodeCount > 0);
            actions.Add(importButton);
            footer.Add(actions);

            statusLabel = new Label(resultMessage ?? (review.errorCount == 0 ? "Ready to import. Review the changes above." : "Resolve all errors before importing."));
            statusLabel.style.marginTop = 5;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            footer.Add(statusLabel);
            root.Add(footer);

            if (review.issues.Count > 0)
            {
                selectedIssue = Mathf.Clamp(selectedIssue, 0, review.issues.Count - 1);
                issueList.SetSelection(selectedIssue);
                BuildDetails();
            }
            else
            {
                selectedIssue = -1;
                BuildDetails();
            }
        }

        private void OnIssueSelected(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                selectedIssue = review.issues.IndexOf((DialogueCsvIssue)item);
                break;
            }

            BuildDetails();
        }

        private void BuildDetails()
        {
            if (detailPanel == null) return;

            detailPanel.Clear();

            if (selectedIssue < 0 || selectedIssue >= review.issues.Count)
            {
                detailPanel.Add(SectionTitle("Validation complete"));
                detailPanel.Add(new HelpBox("No outstanding problems were detected. Review the asset changes and import options before applying.", HelpBoxMessageType.Info));
                return;
            }

            DialogueCsvIssue issue = review.issues[selectedIssue];
            detailPanel.Add(SectionTitle(issue.title));
            detailPanel.Add(new Label(issue.line > 0 ? $"Physical CSV line {issue.line}" : "Whole file"));
            detailPanel.Add(new HelpBox(issue.detail, issue.warning ? HelpBoxMessageType.Info : HelpBoxMessageType.Error));
            detailPanel.Add(Spacer(9));

            if (issue.row < 0)
            {
                detailPanel.Add(new Label("Change the import options or edit the source CSV below."));
                AddRawEditor();
                return;
            }

            if (issue.column < 0)
            {
                detailPanel.Add(new Label("Edit the CSV to resolve the conflict between these rows."));
                AddRawEditor();
                return;
            }

            string column = issue.row == 0 ? $"Header column {issue.column + 1}" : review.GetColumnName(issue.column);
            string original = review.GetCell(issue.row, issue.column);
            TextField value = new($"{column}") { value = original, multiline = original.Contains("\n") };
            value.style.minWidth = 290;
            value.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return && !value.multiline) CommitCell(issue, value.value);
            });
            detailPanel.Add(value);
            detailPanel.Add(new Button(() => CommitCell(issue, value.value)) { text = "Apply correction and revalidate" });
            detailPanel.Add(Spacer(8));

            if (issue.column == 0 && issue.row > 0)
                detailPanel.Add(new Button(() => CommitCell(issue, Guid.NewGuid().ToString())) { text = "Generate unique ID" });

            if (issue.column == 2) AddNodePicker(issue);

            if (issue.column == 5 || issue.column == 6) AddTablePicker(issue);

            string field = review.GetColumnName(issue.column);

            if (field == "SpeakerEntryId" || field == "TextEntryId" || field == "SpeakerKey" || field == "TextKey") AddEntryPicker(issue, field.StartsWith("Speaker", StringComparison.Ordinal));

            if (issue.column == 1) AddTypePicker(issue);

            if (issue.column == 9 || issue.column == 10)
                detailPanel.Add(new Button(() => CommitCell(issue, "")) { text = "Use automatic positioning" });

            if (issue.column >= 0 && review.GetColumnName(issue.column).EndsWith("TableGuid", StringComparison.Ordinal))
                detailPanel.Add(new Button(() => CommitCell(issue, "")) { text = "Use table name instead" });

            if (issue.title == "Translation would be ignored")
                detailPanel.Add(new Button(() => CommitCell(issue, "1")) { text = "Include this translation" });

            if (issue.alternateRow >= 0)
            {
                detailPanel.Add(new Label($"Current value: {Preview(original)}"));
                detailPanel.Add(new Button(() => CommitCell(issue, issue.alternateValue)) { text = $"Use value from line {GetLine(issue.alternateRow)}" });
            }
        }

        private void AddNodePicker(DialogueCsvIssue issue)
        {
            detailPanel.Add(SectionTitle("Connect to existing node"));
            TextField search = new("Search node ID or type");
            detailPanel.Add(search);
            ScrollView matches = new();
            matches.style.maxHeight = 150;
            detailPanel.Add(matches);
            search.RegisterValueChangedCallback(evt => PopulateNodes(issue, matches, evt.newValue));
            PopulateNodes(issue, matches, "");
            detailPanel.Add(new Button(() => CommitCell(issue, "")) { text = "Remove connection" });
        }

        private void PopulateNodes(DialogueCsvIssue issue, ScrollView panel, string query)
        {
            panel.Clear();
            int shown = 0;

            foreach (string label in review.nodeLabels)
            {
                if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                int separator = label.LastIndexOf(" · ", StringComparison.Ordinal);
                string id = label.Substring(separator + 3);
                panel.Add(new Button(() => CommitCell(issue, id)) { text = label });
                shown++;

                if (shown == 30) break;
            }

            if (shown == 0) panel.Add(new Label("No matching nodes."));
        }

        private void AddTablePicker(DialogueCsvIssue issue)
        {
            detailPanel.Add(SectionTitle("Select existing String Table"));
            TextField search = new("Search table name");
            detailPanel.Add(search);
            ScrollView matches = new();
            matches.style.maxHeight = 130;
            detailPanel.Add(matches);
            search.RegisterValueChangedCallback(evt => PopulateTables(issue, matches, evt.newValue));
            PopulateTables(issue, matches, "");
        }

        private void PopulateTables(DialogueCsvIssue issue, ScrollView panel, string query)
        {
            panel.Clear();

            foreach (string name in review.tableNames)
            {
                if (name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                panel.Add(new Button(() => CommitCell(issue, name)) { text = name });
            }
        }

        private void AddEntryPicker(DialogueCsvIssue issue, bool speaker)
        {
            int owner = issue.row;

            if (!speaker && review.GetCell(issue.row, 1) == "Option")
            {
                for (int i = issue.row - 1; i > 0; i--)
                {
                    if (!DialogueCsvImporter.IsNodeType(review.GetCell(i, 1))) continue;

                    owner = i;
                    break;
                }
            }

            string tableName = review.GetCell(owner, speaker ? 5 : 6).Trim();
            IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
            StringTableCollection collection = null;

            foreach (StringTableCollection table in collections)
            {
                if (table.TableCollectionName != tableName) continue;

                collection = table;
                break;
            }

            if (collection == null) return;

            detailPanel.Add(SectionTitle("Select existing Localization entry"));
            TextField search = new("Search key or entry ID");
            detailPanel.Add(search);
            ScrollView matches = new();
            matches.style.maxHeight = 155;
            detailPanel.Add(matches);
            StringTableCollection selectedCollection = collection;
            search.RegisterValueChangedCallback(evt => PopulateEntries(issue, speaker, selectedCollection, matches, evt.newValue));
            PopulateEntries(issue, speaker, selectedCollection, matches, "");
        }

        private void PopulateEntries(DialogueCsvIssue issue, bool speaker, StringTableCollection collection, ScrollView panel, string query)
        {
            panel.Clear();
            int shown = 0;

            foreach (SharedTableData.SharedTableEntry entry in collection.SharedData.Entries)
            {
                string label = $"{entry.Key} · {entry.Id}";

                if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                SharedTableData.SharedTableEntry selectedEntry = entry;
                panel.Add(new Button(() =>
                {
                    review.SetEntryReference(issue.row, speaker, selectedEntry.Key, selectedEntry.Id);
                    Rescan(false);
                }) { text = label });
                shown++;

                if (shown == 30) break;
            }

            if (shown == 0) panel.Add(new Label("No matching entries."));
        }

        private void AddTypePicker(DialogueCsvIssue issue)
        {
            detailPanel.Add(SectionTitle("Choose node type"));
            string[] types = { "Start", "Dialogue", "Choice", "Condition", "Trigger", "Event", "Command", "Wait", "Option", "Branch", "Instruction" };
            VisualElement buttons = new();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;

            foreach (string type in types) buttons.Add(new Button(() => CommitCell(issue, type)) { text = type });

            detailPanel.Add(buttons);
        }

        private void AddRawEditor()
        {
            Foldout advanced = new() { text = "Edit raw CSV", value = !review.parsed };
            TextField raw = new("CSV text") { value = review.csv, multiline = true };
            raw.style.minHeight = 210;
            advanced.Add(raw);
            advanced.Add(new Button(() =>
            {
                review.csv = raw.value;
                Rescan();
            }) { text = "Reparse CSV" });
            detailPanel.Add(advanced);
        }

        private void AddToggle(VisualElement panel, string name, bool selected, Action<bool> change)
        {
            Toggle toggle = new(name) { value = selected };
            toggle.style.marginRight = 14;
            toggle.RegisterValueChangedCallback(evt =>
            {
                change(evt.newValue);
                Rescan();
            });
            panel.Add(toggle);
        }

        private void ChooseFolder()
        {
            string chosen = EditorUtility.OpenFolderPanel("New String Table folder", Application.dataPath, "");

            if (string.IsNullOrEmpty(chosen)) return;

            string assets = Application.dataPath.Replace('\\', '/');
            chosen = chosen.Replace('\\', '/');

            if (chosen != assets && !chosen.StartsWith(assets + "/", StringComparison.Ordinal))
            {
                resultMessage = "Choose a folder inside this project's Assets directory.";
                BuildWindow();
                return;
            }

            review.options.tableFolder = "Assets" + chosen.Substring(assets.Length);
            Rescan();
        }

        private void CommitCell(DialogueCsvIssue issue, string value)
        {
            review.SetCell(issue.row, issue.column, value);
            Rescan(false);
        }

        private void Rescan(bool scan = true)
        {
            if (scan) review.Scan();

            resultMessage = null;
            selectedIssue = 0;
            BuildWindow();
        }

        private void SaveCsv()
        {
            string folder = Path.GetDirectoryName(sourcePath);
            string filename = Path.GetFileNameWithoutExtension(sourcePath) + ".csv";
            string path = EditorUtility.SaveFilePanel("Save corrected Dialogue CSV", folder, filename, "csv");

            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, review.csv, new UTF8Encoding(true));
            sourcePath = path;
            resultMessage = "Saved corrected CSV: " + path;
            BuildWindow();
        }

        private void ApplyImport()
        {
            review.Scan();

            if (review.errorCount != 0 || !review.parsed)
            {
                BuildWindow();
                return;
            }

            if (!DialogueCsvImporter.Import(asset, review.csv, review.options, out string message))
            {
                resultMessage = message;
                BuildWindow();
                return;
            }

            DialogueEditorWindow editor = DialogueEditorWindow.Open();
            editor.SO = asset;
            Close();

            if (!string.IsNullOrEmpty(message)) EditorUtility.DisplayDialog("CSV Import Notices", message, "OK");
        }

        private bool HasCsvStart()
        {
            for (int i = 1; i < review.rows.Count; i++)
            {
                if (review.GetCell(i, 1) == "Start") return true;
            }

            return false;
        }

        private int GetLine(int row)
        {
            // The issue itself already contains the first physical line; CSV rows may span multiple lines.
            CsvUtility.FromCsv(review.csv, out List<int> lines);

            return row >= 0 && row < lines.Count ? lines[row] : 0;
        }

        private static void AddChangeDetails(VisualElement parent, string title, IReadOnlyCollection<string> changes)
        {
            if (changes.Count == 0) return;

            parent.Add(SectionTitle(title));
            int shown = 0;

            foreach (string item in changes)
            {
                parent.Add(new Label(item));
                shown++;

                if (shown == 30) break;
            }

            if (changes.Count > shown) parent.Add(new Label($"...and {changes.Count - shown} more"));
        }

        private static VisualElement Statistic(string text)
        {
            Label label = new(text);
            label.style.marginRight = 16;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            return label;
        }

        private static Label SectionTitle(string value)
        {
            Label label = new(value);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 5;
            label.style.marginTop = 3;

            return label;
        }

        private static VisualElement Spacer(int height)
        {
            VisualElement spacer = new();
            spacer.style.height = height;

            return spacer;
        }

        private static string Preview(string text)
        {
            text = text.Replace('\n', ' ').Replace('\r', ' ');

            return text.Length > 100 ? text.Substring(0, 100) + "..." : text;
        }
    }
}
