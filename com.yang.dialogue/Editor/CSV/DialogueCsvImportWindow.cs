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
        private readonly List<IssueGroup> issueGroups = new();
        private readonly List<IssueGroup> visibleGroups = new();
        private int selectedGroup = -1;
        private int selectedIssue;
        private string searchQuery = "";
        private string preferredGroupKey;
        private string savedCsv;
        private bool showNotices = true;
        private bool optionsExpanded;
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
            window.savedCsv = csv;
            window.review = new DialogueCsvImportReview(so, csv);
            window.titleContent = new GUIContent("Dialogue CSV Import");
            window.minSize = new Vector2(920, 660);
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
            BuildIssueGroups();
            BuildVisibleGroups();
            int errorGroups = 0;
            int warningGroups = 0;

            foreach (IssueGroup group in issueGroups)
            {
                if (group.warning) warningGroups++;
                else errorGroups++;
            }

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
            Label introduction = new("The imported file is authoritative. Valid changes are applied automatically; only unresolved decisions are shown below.");
            introduction.style.whiteSpace = WhiteSpace.Normal;
            root.Add(introduction);
            root.Add(Spacer(8));

            VisualElement statistics = new();
            statistics.style.flexDirection = FlexDirection.Row;
            statistics.style.flexWrap = Wrap.Wrap;
            statistics.style.paddingLeft = 8;
            statistics.style.paddingTop = 7;
            statistics.style.paddingBottom = 7;
            statistics.style.marginBottom = 9;
            statistics.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.19f, 0.21f, 0.23f) : new Color(0.91f, 0.93f, 0.95f);
            statistics.Add(Statistic($"{errorGroups} decisions ({review.errorCount} occurrences)"));
            statistics.Add(Statistic($"{warningGroups} notices"));
            statistics.Add(Statistic($"{review.nodeCount} nodes"));
            statistics.Add(Statistic($"{review.detailCount} detail rows"));
            root.Add(statistics);

            Foldout advancedOptions = new() { text = "Import options · creation and fallback", value = optionsExpanded };
            advancedOptions.RegisterValueChangedCallback(evt => optionsExpanded = evt.newValue);
            advancedOptions.style.marginBottom = 6;
            VisualElement settings = new();
            settings.style.flexDirection = FlexDirection.Row;
            settings.style.flexWrap = Wrap.Wrap;
            settings.style.marginBottom = 6;
            AddToggle(settings, "Create missing tables", review.options.createTables, value => review.options.createTables = value);
            AddToggle(settings, "Create missing entries", review.options.createEntries, value => review.options.createEntries = value);
            AddToggle(settings, "Create missing locale tables", review.options.createLocaleTables, value => review.options.createLocaleTables = value);
            AddToggle(settings, "Create nodes with unknown IDs", review.options.createNodes, value => review.options.createNodes = value);
            AddToggle(settings, "Use asset default tables", review.options.useDefaultTables, value => review.options.useDefaultTables = value);
            AddToggle(settings, "Keep existing Start if missing", review.options.keepStart, value => review.options.keepStart = value);
            advancedOptions.Add(settings);

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
                advancedOptions.Add(folderRow);
            }

            root.Add(advancedOptions);

            TwoPaneSplitView panes = new(0, 330, TwoPaneSplitViewOrientation.Horizontal);
            panes.style.flexGrow = 1;
            panes.style.minHeight = 290;

            VisualElement left = new();
            left.style.flexGrow = 1;
            left.Add(SectionTitle("Resolve CSV issues"));
            TextField search = new("Search") { value = searchQuery };
            search.tooltip = "Search by issue, Localization reference, field, line, or description.";
            search.RegisterValueChangedCallback(evt => UpdateSearch(evt.newValue));
            left.Add(search);
            Toggle notices = new("Show notices") { value = showNotices };
            notices.style.marginBottom = 6;
            notices.RegisterValueChangedCallback(evt =>
            {
                showNotices = evt.newValue;
                UpdateVisibleIssues();
            });
            left.Add(notices);
            issueList = new ListView();
            issueList.itemsSource = visibleGroups;
            issueList.fixedItemHeight = 60;
            issueList.showBorder = true;
            issueList.selectionType = SelectionType.Single;
            issueList.makeItem = () =>
            {
                VisualElement item = new();
                item.style.paddingLeft = 8;
                item.style.paddingRight = 6;
                item.style.paddingTop = 5;
                Label heading = new() { name = "heading" };
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                heading.style.overflow = Overflow.Hidden;
                item.Add(heading);
                Label subtitle = new() { name = "subtitle" };
                subtitle.style.fontSize = 11;
                subtitle.style.whiteSpace = WhiteSpace.NoWrap;
                subtitle.style.overflow = Overflow.Hidden;
                item.Add(subtitle);

                return item;
            };
            issueList.bindItem = (item, index) =>
            {
                IssueGroup group = visibleGroups[index];
                DialogueCsvIssue issue = group.items[0];
                int affected = review.GetAffectedRows(issue).Count;
                int count = Math.Max(group.items.Count, affected);
                string location = string.IsNullOrEmpty(issue.referenceLabel) ? GetIssueLocation(issue) : issue.referenceLabel;
                item.Q<Label>("heading").text = $"{(group.warning ? "NOTICE" : "FIX")} · {group.title}";
                item.Q<Label>("subtitle").text = $"{location} · {count} affected";
                item.tooltip = group.title + "\n" + issue.detail;
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
            footer.style.marginTop = 10;
            footer.style.paddingTop = 8;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = EditorGUIUtility.isProSkin ? new Color(0.34f, 0.36f, 0.39f) : new Color(0.7f, 0.72f, 0.75f);
            footer.Add(SectionTitle("Changes to apply"));
            footer.Add(new Label($"Graph: {review.currentNodes} → {review.nodeCount + (HasCsvStart() ? 0 : 1)} nodes, {review.currentLinks} → up to {review.linkCount + (HasCsvStart() ? 0 : 1)} links"));
            footer.Add(new Label($"Localization: {review.newTableCount} new tables, {review.newEntryCount} new entries, {review.tableRenameCount} table renames, {review.renameCount} key renames, {review.translationCount} translated values to write"));
            footer.Add(new Label("Import rebuilds the graph and writes CSV values. Review before-and-after values before confirming."));
            Button previewButton = new(ShowChangePreview) { text = "Preview changes (before → after)..." };
            previewButton.style.alignSelf = Align.FlexStart;
            previewButton.style.marginTop = 5;
            previewButton.SetEnabled(HasPreviewStructure());
            footer.Add(previewButton);

            VisualElement actions = new();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 8;
            actions.Add(new Button(SaveCorrections) { text = "Save corrections..." });
            actions.Add(new Button(CancelReview) { text = "Cancel" });
            VisualElement push = new();
            push.style.flexGrow = 1;
            actions.Add(push);
            if (review.errorCount > 0)
            {
                Button ignoreButton = new(ApplyImportIgnoringErrors) { text = "Ignore invalid data and import" };
                ignoreButton.SetEnabled(review.parsed && review.nodeCount > 0);
                actions.Add(ignoreButton);
            }

            importButton = new Button(ApplyImport) { text = "Replace graph and import" };
            importButton.SetEnabled(review.errorCount == 0 && review.parsed && review.nodeCount > 0);
            actions.Add(importButton);
            footer.Add(actions);

            string editStatus = review.csv == savedCsv ? "" : "CSV edits are not saved to disk.  ";
            statusLabel = new Label(editStatus + (resultMessage ?? (review.errorCount == 0 ? "Ready to import. Review the changes above." : "Resolve the outstanding decisions, or explicitly ignore invalid data.")));
            statusLabel.style.marginTop = 5;
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            footer.Add(statusLabel);
            root.Add(footer);

            RestoreSelection();
        }

        private void OnIssueSelected(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                int index = visibleGroups.IndexOf((IssueGroup)item);

                if (selectedGroup != index) selectedIssue = 0;
                selectedGroup = index;
                break;
            }

            BuildDetails();
        }

        private void BuildDetails()
        {
            if (detailPanel == null) return;

            detailPanel.Clear();

            if (selectedGroup < 0 || selectedGroup >= visibleGroups.Count)
            {
                detailPanel.Add(SectionTitle(visibleGroups.Count == 0 && issueGroups.Count != 0 ? "No matching issues" : "Validation complete"));
                detailPanel.Add(new HelpBox(visibleGroups.Count == 0 && issueGroups.Count != 0 ? "Change the search or turn on notices to view the remaining issues." : "No outstanding problems were detected. Review the asset changes and import options before applying.", HelpBoxMessageType.Info));
                return;
            }

            IssueGroup group = visibleGroups[selectedGroup];
            selectedIssue = Mathf.Clamp(selectedIssue, 0, group.items.Count - 1);
            DialogueCsvIssue issue = group.items[selectedIssue];
            detailPanel.Add(SectionTitle(group.title));
            detailPanel.Add(new Label(group.warning ? "Notice · import may proceed" : "Action required before a normal import"));

            if (issue.row > 0)
            {
                string type = review.GetCell(issue.row, 1);
                string nodeId = review.GetCell(issue.row, 0);
                string context = DialogueCsvImporter.IsNodeType(type) ? $"{type} · {nodeId}" : $"{type} detail";
                detailPanel.Add(new Label($"CSV line {issue.line}  |  {context}"));
            }

            AddIssueNavigation();

            if (!IsQuickConflict(issue.title)) AddIssueVariants(group);

            if (!string.IsNullOrEmpty(issue.referenceScope))
            {
                List<int> affected = review.GetAffectedRows(issue);
                detailPanel.Add(new Label(issue.referenceLabel));
                detailPanel.Add(new Label($"Editing this reference updates {affected.Count} matching CSV row(s)."));

                if (affected.Count > 1)
                {
                    detailPanel.Add(new HelpBox($"These {affected.Count} CSV rows share this Localization issue. One correction updates all matching rows.", HelpBoxMessageType.Info));
                    Foldout locations = new() { text = $"Affected CSV rows ({affected.Count})", value = false };
                    int shown = 0;

                    foreach (int row in affected)
                    {
                        locations.Add(new Label($"Line {GetLine(row)} · {review.GetAffectedFieldName(issue, row)}"));
                        shown++;

                        if (shown == 30) break;
                    }

                    if (affected.Count > shown) locations.Add(new Label($"...and {affected.Count - shown} more"));

                    detailPanel.Add(locations);
                }
                else detailPanel.Add(new Label(GetIssueLocation(issue)));
            }
            else detailPanel.Add(new Label(GetIssueLocation(issue)));

            detailPanel.Add(new HelpBox(issue.detail, issue.warning ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning));
            detailPanel.Add(Spacer(9));

            if (AddQuickConflictChoices(issue)) return;

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

            if (issue.title == "Locale table missing")
            {
                detailPanel.Add(new Button(() =>
                {
                    review.options.createLocaleTables = true;
                    Rescan();
                }) { text = "Create all missing locale tables" });
                return;
            }

            detailPanel.Add(SectionTitle("Choose a resolution"));

            if (issue.column == 0 && issue.row > 0)
            {
                detailPanel.Add(new Button(() => CommitCell(issue, Guid.NewGuid().ToString())) { text = "Generate unique ID" });

                if (issue.title == "New node ID")
                {
                    detailPanel.Add(new Button(() =>
                    {
                        review.ApproveNewNode(issue);
                        RefreshAfterCorrection(issue);
                    }) { text = "Create new node with CSV ID" });
                    AddExistingNodePicker(issue);
                }
            }

            if (issue.title == "Unused data for Type")
                detailPanel.Add(new Button(() => CommitCell(issue, "")) { text = "Clear unused field" });

            if (issue.title == "Missing Option")
                detailPanel.Add(new Button(() =>
                {
                    review.InsertOption(issue.row);
                    RefreshAfterCorrection(issue);
                }) { text = "Add an Option row" });

            if (issue.title == "Option table GUID mismatch")
            {
                detailPanel.Add(new Button(() =>
                {
                    review.SyncChoiceTableGuid(issue.row, true);
                    RefreshAfterCorrection(issue);
                }) { text = "Use Option GUID for this Choice" });
                detailPanel.Add(new Button(() =>
                {
                    review.SyncChoiceTableGuid(issue.row, false);
                    RefreshAfterCorrection(issue);
                }) { text = "Use parent Choice GUID" });
            }

            if (issue.column == 2) AddNodePicker(issue);

            string field = review.GetColumnName(issue.column);

            if (issue.column == 5 || issue.column == 6 || field == "SpeakerTableGuid" || field == "TextTableGuid")
            {
                AddTablePicker(issue);

                if (issue.title == "Missing table GUID" || issue.title == "Missing Localization table")
                {
                    bool speakerTable = issue.column == 5 || field == "SpeakerTableGuid";
                    detailPanel.Add(new Button(() =>
                    {
                        review.CreateTableForReference(issue, speakerTable);
                        RefreshAfterCorrection(issue);
                    }) { text = "Create a new table using the CSV name" });
                    detailPanel.Add(new HelpBox("Unity generates the GUID when creating a String Table. A missing GUID cannot be reused; the generated GUID will be exported on the next CSV export.", HelpBoxMessageType.Info));
                }
            }

            if (issue.title == "Missing Localization entry ID" || issue.title == "Missing Localization entry")
                detailPanel.Add(new Button(() =>
                {
                    review.options.createEntries = true;
                    Rescan();
                }) { text = "Create all missing Localization entries from CSV" });

            if (field == "SpeakerEntryId" || field == "TextEntryId" || field == "SpeakerKey" || field == "TextKey") AddEntryPicker(issue, field.StartsWith("Speaker", StringComparison.Ordinal));

            if (issue.column == 1) AddTypePicker(issue);

            if (issue.column == 9 || issue.column == 10)
                detailPanel.Add(new Button(() => CommitCell(issue, "")) { text = "Use automatic positioning" });

            if (issue.alternateRow >= 0)
            {
                detailPanel.Add(new Button(() => CommitCell(issue, issue.alternateValue)) { text = $"Use value from line {GetLine(issue.alternateRow)}" });
            }

            string column = issue.row == 0 ? $"Header column {issue.column + 1}" : review.GetColumnName(issue.column);
            string original = review.GetCell(issue.row, issue.column);
            detailPanel.Add(SectionTitle("Edit CSV value"));
            TextField value = new($"{column}") { value = original, multiline = original.Contains("\n") };
            value.style.minWidth = 290;
            value.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return && !value.multiline) CommitCell(issue, value.value);
            });
            detailPanel.Add(value);
            detailPanel.Add(new Button(() => CommitCell(issue, value.value)) { text = "Apply to matching rows and revalidate" });
            detailPanel.Add(Spacer(8));

        }

        private static bool IsQuickConflict(string title)
        {
            return title == "Conflicting entry keys" || title == "Conflicting entry IDs" ||
                title == "Conflicting translation" || title == "Conflicting table names";
        }

        private bool AddQuickConflictChoices(DialogueCsvIssue issue)
        {
            if (!IsQuickConflict(issue.title)) return false;

            List<DialogueCsvConflictChoice> choices = review.GetConflictChoices(issue);

            if (choices.Count < 2) return false;

            string label = issue.title switch
            {
                "Conflicting entry keys" => "Key",
                "Conflicting entry IDs" => "Entry ID",
                "Conflicting table names" => "table name",
                _ => "translation",
            };
            int affected = review.GetAffectedRows(issue).Count;

            detailPanel.Add(SectionTitle($"Choose the final {label}"));
            Label instruction = new($"Select one value to update all {affected} matching CSV rows immediately. The list is sorted by usage count.");
            instruction.style.whiteSpace = WhiteSpace.Normal;
            detailPanel.Add(instruction);
            bool hasCurrent = review.TryGetCurrentConflictValue(issue, out string current);

            if (hasCurrent)
            {
                Label existing = new($"Current Localization value: {DisplayChoice(current)}");
                existing.style.whiteSpace = WhiteSpace.Normal;
                detailPanel.Add(existing);
            }

            detailPanel.Add(Spacer(6));
            bool includesCurrent = false;

            foreach (DialogueCsvConflictChoice choice in choices)
            {
                bool currentValue = hasCurrent && choice.value == current;
                includesCurrent |= currentValue;
                string selected = choice.value;
                string suffix = currentValue ? " · current" : "";
                string buttonText = $"Use {DisplayChoice(selected)}   ·   {choice.count} reference(s){suffix}";
                Button button = new(() => CommitCell(issue, selected)) { text = buttonText, tooltip = selected };
                button.style.marginBottom = 4;
                button.style.minHeight = 27;

                if (issue.title != "Conflicting translation" && string.IsNullOrWhiteSpace(selected)) button.SetEnabled(false);

                detailPanel.Add(button);
            }

            if (hasCurrent && !includesCurrent && (issue.title == "Conflicting translation" || !string.IsNullOrWhiteSpace(current)))
            {
                string selected = current;
                Button keepCurrent = new(() => CommitCell(issue, selected)) { text = $"Use current Localization value · {DisplayChoice(selected)}", tooltip = selected };
                keepCurrent.style.marginTop = 4;
                detailPanel.Add(keepCurrent);
            }

            Foldout custom = new() { text = "Enter another value", value = false };
            string original = review.GetCell(issue.row, issue.column);
            TextField input = new(review.GetColumnName(issue.column)) { value = original, multiline = issue.title == "Conflicting translation" || original.Contains("\n") };
            input.style.minWidth = 280;
            custom.Add(input);
            custom.Add(new Button(() => CommitCell(issue, input.value)) { text = "Apply to all matching rows" });
            custom.style.marginTop = 8;
            detailPanel.Add(custom);

            return true;
        }

        private static string DisplayChoice(string value)
        {
            if (string.IsNullOrEmpty(value)) return "(empty)";

            string label = value.Replace('\n', ' ').Replace('\r', ' ');

            return label.Length > 64 ? label.Substring(0, 64) + "..." : label;
        }

        private void AddExistingNodePicker(DialogueCsvIssue issue)
        {
            Foldout picker = new() { text = "Reconnect to an existing node ID", value = true };
            TextField search = new("Search existing nodes");
            picker.Add(search);
            ScrollView matches = new();
            matches.style.maxHeight = 160;
            picker.Add(matches);
            search.RegisterValueChangedCallback(evt => PopulateExistingNodes(issue, matches, evt.newValue));
            PopulateExistingNodes(issue, matches, "");
            detailPanel.Add(picker);
        }

        private void PopulateExistingNodes(DialogueCsvIssue issue, ScrollView panel, string query)
        {
            panel.Clear();
            HashSet<string> csvIds = new(StringComparer.Ordinal);

            for (int row = 1; row < review.rows.Count; row++)
            {
                if (DialogueCsvImporter.IsNodeType(review.GetCell(row, 1))) csvIds.Add(review.GetCell(row, 0).Trim());
            }

            int found = 0;
            int shown = 0;

            foreach (NodeData node in asset.EditorNodes)
            {
                if (string.IsNullOrEmpty(node.guid) || csvIds.Contains(node.guid)) continue;

                string label = $"{node.type} · {node.guid}";

                if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                found++;

                if (shown >= 30) continue;

                string id = node.guid;
                panel.Add(new Button(() =>
                {
                    review.ReplaceNodeId(issue, id);
                    RefreshAfterCorrection(issue);
                }) { text = label });
                shown++;
            }

            AddPickerCount(panel, found, shown);
        }

        private void AddNodePicker(DialogueCsvIssue issue)
        {
            Foldout picker = new() { text = "Connect Next to an imported node", value = true };
            TextField search = new("Search node ID or type");
            picker.Add(search);
            ScrollView matches = new();
            matches.style.maxHeight = 150;
            picker.Add(matches);
            search.RegisterValueChangedCallback(evt => PopulateNodes(issue, matches, evt.newValue));
            PopulateNodes(issue, matches, "");
            picker.Add(new Button(() => CommitCell(issue, "")) { text = "Clear Next connection" });
            detailPanel.Add(picker);
        }

        private void PopulateNodes(DialogueCsvIssue issue, ScrollView panel, string query)
        {
            panel.Clear();
            int shown = 0;
            int found = 0;

            for (int row = 1; row < review.rows.Count; row++)
            {
                string type = review.GetCell(row, 1);

                if (!DialogueCsvImporter.IsNodeType(type)) continue;

                string id = review.GetCell(row, 0).Trim();
                string label = $"Line {review.GetLine(row)} · {type} · {id}";

                if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                found++;

                if (shown >= 30) continue;

                string selectedId = id;
                panel.Add(new Button(() => CommitCell(issue, selectedId)) { text = label });
                shown++;
            }

            AddPickerCount(panel, found, shown);
        }

        private void AddTablePicker(DialogueCsvIssue issue)
        {
            Foldout picker = new() { text = "Connect to an existing String Table", value = true };
            TextField search = new("Search table name or GUID");
            picker.Add(search);
            ScrollView matches = new();
            matches.style.maxHeight = 130;
            picker.Add(matches);
            detailPanel.Add(picker);
            search.RegisterValueChangedCallback(evt => PopulateTables(issue, matches, evt.newValue));
            PopulateTables(issue, matches, "");
        }

        private void PopulateTables(DialogueCsvIssue issue, ScrollView panel, string query)
        {
            panel.Clear();
            int found = 0;
            int shown = 0;
            IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();

            foreach (StringTableCollection collection in collections)
            {
                string guid = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString("N");
                string label = $"{collection.TableCollectionName} · {guid}";

                if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                found++;

                if (shown >= 30) continue;

                bool speaker = review.GetColumnName(issue.column).StartsWith("Speaker", StringComparison.Ordinal);
                StringTableCollection selected = collection;
                panel.Add(new Button(() =>
                {
                    review.SetTableReference(issue, speaker, selected);
                    RefreshAfterCorrection(issue);
                }) { text = label });
                shown++;
            }

            AddPickerCount(panel, found, shown);
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

            string tableName = review.GetReferenceTable(issue);

            if (string.IsNullOrEmpty(tableName)) tableName = review.GetCell(owner, speaker ? 5 : 6).Trim();

            int tableGuidColumn = review.GetColumnIndex(speaker ? "SpeakerTableGuid" : "TextTableGuid");
            string tableGuid = review.GetCell(owner, tableGuidColumn).Trim();
            bool hasGuid = Guid.TryParse(tableGuid, out Guid guid);
            IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
            StringTableCollection collection = null;

            foreach (StringTableCollection table in collections)
            {
                if (hasGuid && table.TableCollectionNameReference.TableCollectionNameGuid != guid) continue;
                if (!hasGuid && table.TableCollectionName != tableName) continue;

                collection = table;
                break;
            }

            if (collection == null) return;

            Foldout picker = new() { text = $"Connect to an existing entry · {collection.TableCollectionName}", value = true };
            TextField search = new("Search key or entry ID");
            picker.Add(search);
            ScrollView matches = new();
            matches.style.maxHeight = 155;
            picker.Add(matches);
            detailPanel.Add(picker);
            StringTableCollection selectedCollection = collection;
            search.RegisterValueChangedCallback(evt => PopulateEntries(issue, speaker, selectedCollection, matches, evt.newValue));
            PopulateEntries(issue, speaker, selectedCollection, matches, "");
        }

        private void PopulateEntries(DialogueCsvIssue issue, bool speaker, StringTableCollection collection, ScrollView panel, string query)
        {
            panel.Clear();
            int shown = 0;
            int found = 0;

            foreach (SharedTableData.SharedTableEntry entry in collection.SharedData.Entries)
            {
                string label = $"{entry.Key} · {entry.Id}";

                if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                found++;

                if (shown >= 30) continue;

                SharedTableData.SharedTableEntry selectedEntry = entry;
                panel.Add(new Button(() =>
                {
                    review.SetEntryReference(issue, speaker, selectedEntry.Key, selectedEntry.Id);
                    RefreshAfterCorrection(issue);
                }) { text = label });
                shown++;
            }

            AddPickerCount(panel, found, shown);
        }

        private void AddTypePicker(DialogueCsvIssue issue)
        {
            Foldout picker = new() { text = "Choose a valid Type", value = true };
            picker.Add(SectionTitle("Node types"));
            string[] nodeTypes = { "Start", "Dialogue", "Choice", "Condition", "Trigger", "Event", "Command", "Wait" };
            VisualElement nodes = new();
            nodes.style.flexDirection = FlexDirection.Row;
            nodes.style.flexWrap = Wrap.Wrap;

            foreach (string type in nodeTypes) nodes.Add(new Button(() => CommitCell(issue, type)) { text = type });

            picker.Add(nodes);
            string parent = "";

            for (int row = issue.row - 1; row > 0; row--)
            {
                string type = review.GetCell(row, 1);

                if (!DialogueCsvImporter.IsNodeType(type)) continue;

                parent = type;
                break;
            }

            string detailType = parent switch
            {
                "Choice" => "Option",
                "Condition" => "Branch",
                "Command" => "Instruction",
                "Event" => "Instruction",
                _ => "",
            };

            if (!string.IsNullOrEmpty(detailType))
            {
                picker.Add(SectionTitle($"Detail type for {parent}"));
                picker.Add(new Button(() => CommitCell(issue, detailType)) { text = detailType });
            }

            detailPanel.Add(picker);
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
            review.SetCell(issue, value);
            RefreshAfterCorrection(issue);
        }

        private void RefreshAfterCorrection(DialogueCsvIssue corrected)
        {
            preferredGroupKey = selectedGroup >= 0 && selectedGroup < visibleGroups.Count ? visibleGroups[selectedGroup].key : null;
            resultMessage = "CSV correction applied and revalidated. Check any remaining decisions before import.";
            BuildWindow();
        }

        private void Rescan(bool scan = true)
        {
            if (scan) review.Scan();

            preferredGroupKey = selectedGroup >= 0 && selectedGroup < visibleGroups.Count ? visibleGroups[selectedGroup].key : null;
            resultMessage = null;
            BuildWindow();
        }

        private void CancelReview()
        {
            if (review.csv != savedCsv && !EditorUtility.DisplayDialog("Discard CSV corrections?", "The corrections made in this review have not been saved to disk. Discard them?", "Discard", "Continue editing")) return;

            Close();
        }

        private void SaveCorrections()
        {
            bool excel = Path.GetExtension(sourcePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
            string extension = excel ? "xlsx" : "csv";
            string folder = Path.GetDirectoryName(sourcePath);
            string filename = Path.GetFileNameWithoutExtension(sourcePath) + "." + extension;
            string path = EditorUtility.SaveFilePanel("Save corrected Dialogue " + extension.ToUpperInvariant(), folder, filename, extension);

            if (string.IsNullOrEmpty(path)) return;

            try
            {
                if (excel) DialogueExcelUtility.Save(path, CsvUtility.FromCsv(review.csv));
                else File.WriteAllText(path, review.csv, new UTF8Encoding(true));
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Save corrections failed", exception.Message, "OK");
                return;
            }

            sourcePath = path;
            savedCsv = review.csv;
            resultMessage = "Saved corrections: " + path;
            BuildWindow();
        }

        private bool HasPreviewStructure()
        {
            string[] required = { "ID", "Type", "Next", "Message", "Data", "SpeakerTable", "TextTable", "SpeakerKey", "TextKey", "X", "Y" };

            if (!review.parsed || review.rows.Count == 0 || review.rows[0].Count < required.Length) return false;

            for (int i = 0; i < required.Length; i++)
            {
                if (review.GetCell(0, i) != required[i]) return false;
            }

            return true;
        }

        private void ShowChangePreview()
        {
            if (!HasPreviewStructure()) return;

            DialogueCsvImportPreview changes = DialogueCsvImportPreview.Build(asset, review.csv, review.options);
            DialogueCsvImportPreviewWindow.Display(changes, false, false);
        }

        private void ApplyImport()
        {
            review.Scan();

            if (review.errorCount != 0 || !review.parsed)
            {
                BuildWindow();
                return;
            }

            DialogueCsvImportPreview changes = DialogueCsvImportPreview.Build(asset, review.csv, review.options);

            if (!DialogueCsvImportPreviewWindow.Display(changes, true, false)) return;

            if (!DialogueCsvImporter.Import(asset, review.csv, review.options, out string message))
            {
                resultMessage = message;
                BuildWindow();
                return;
            }

            FinishImport(message);
        }

        private void ApplyImportIgnoringErrors()
        {
            if (!DialogueCsvImportSanitizer.TrySanitize(asset, review.csv, review.options, out string sanitized, out DialogueCsvImportOptions effectiveOptions, out string summary, out string error))
            {
                resultMessage = error;
                BuildWindow();
                return;
            }

            DialogueCsvImportPreview changes = DialogueCsvImportPreview.Build(asset, sanitized, effectiveOptions);

            if (!DialogueCsvImportPreviewWindow.Display(changes, true, true, summary)) return;

            if (!DialogueCsvImporter.Import(asset, sanitized, effectiveOptions, out string message))
            {
                resultMessage = message;
                BuildWindow();
                return;
            }

            FinishImport(summary + (string.IsNullOrEmpty(message) ? "" : "\n" + message));
        }

        private void FinishImport(string message)
        {
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
            return review.GetLine(row);
        }

        private void UpdateSearch(string query)
        {
            searchQuery = query ?? "";
            UpdateVisibleIssues();
        }

        private void UpdateVisibleIssues()
        {
            IssueGroup selected = selectedGroup >= 0 && selectedGroup < visibleGroups.Count ? visibleGroups[selectedGroup] : null;
            BuildVisibleGroups();
            issueList.Rebuild();
            selectedGroup = selected == null ? -1 : visibleGroups.IndexOf(selected);
            selectedIssue = 0;

            if (selectedGroup < 0 && visibleGroups.Count > 0) selectedGroup = 0;

            if (selectedGroup >= 0) issueList.SetSelection(selectedGroup);
            else issueList.ClearSelection();

            BuildDetails();
        }

        private void BuildVisibleGroups()
        {
            visibleGroups.Clear();

            foreach (IssueGroup group in issueGroups)
            {
                if (!showNotices && group.warning) continue;

                if (!MatchesSearch(group)) continue;

                visibleGroups.Add(group);
            }
        }

        private bool MatchesSearch(IssueGroup group)
        {
            if (string.IsNullOrWhiteSpace(searchQuery)) return true;

            string query = searchQuery.Trim();

            if (group.title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            foreach (DialogueCsvIssue issue in group.items)
            {
                if (issue.detail.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!string.IsNullOrEmpty(issue.referenceLabel) && issue.referenceLabel.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    GetIssueLocation(issue).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        private void RestoreSelection()
        {
            if (visibleGroups.Count == 0)
            {
                selectedGroup = -1;
                selectedIssue = 0;
                preferredGroupKey = null;
                BuildDetails();
                return;
            }

            if (!string.IsNullOrEmpty(preferredGroupKey))
            {
                for (int i = 0; i < visibleGroups.Count; i++)
                {
                    if (visibleGroups[i].key != preferredGroupKey) continue;

                    selectedGroup = i;
                    break;
                }
            }

            selectedGroup = Mathf.Clamp(selectedGroup, 0, visibleGroups.Count - 1);
            selectedIssue = 0;
            preferredGroupKey = null;
            issueList.SetSelection(selectedGroup);
            BuildDetails();
        }

        private void AddIssueNavigation()
        {
            VisualElement navigation = new();
            navigation.style.flexDirection = FlexDirection.Row;
            navigation.style.marginTop = 6;
            navigation.style.marginBottom = 8;
            Button previous = new(() => SelectIssue(selectedGroup - 1)) { text = "← Previous" };
            previous.SetEnabled(selectedGroup > 0);
            navigation.Add(previous);
            Label progress = new($"  {selectedGroup + 1} / {visibleGroups.Count}  ");
            progress.style.alignSelf = Align.Center;
            navigation.Add(progress);
            Button next = new(() => SelectIssue(selectedGroup + 1)) { text = "Next →" };
            next.SetEnabled(selectedGroup < visibleGroups.Count - 1);
            navigation.Add(next);
            detailPanel.Add(navigation);
        }

        private void SelectIssue(int index)
        {
            if (index < 0 || index >= visibleGroups.Count) return;

            selectedIssue = 0;
            selectedGroup = index;
            issueList.SetSelection(index);
            issueList.ScrollToItem(index);
            BuildDetails();
        }

        private void AddIssueVariants(IssueGroup group)
        {
            if (group.items[0].title == "Locale table missing") return;

            List<int> variants = new();
            HashSet<string> seen = new(StringComparer.Ordinal);

            for (int i = 0; i < group.items.Count; i++)
            {
                DialogueCsvIssue issue = group.items[i];
                string field = review.GetColumnName(issue.column);
                string value = review.GetCell(issue.row, issue.column);
                string key = field + "\u001f" + value;

                if (seen.Add(key)) variants.Add(i);
            }

            if (variants.Count < 2) return;

            Foldout variantsPanel = new() { text = $"Conflicting values ({variants.Count}) · choose one to edit", value = true };
            TextField search = new("Search values or CSV line");
            variantsPanel.Add(search);
            ScrollView matches = new();
            matches.style.maxHeight = 170;
            variantsPanel.Add(matches);
            search.RegisterValueChangedCallback(evt => PopulateIssueVariants(group, variants, matches, evt.newValue));
            PopulateIssueVariants(group, variants, matches, "");
            detailPanel.Add(variantsPanel);
        }

        private void PopulateIssueVariants(IssueGroup group, List<int> variants, ScrollView panel, string query)
        {
            panel.Clear();
            int found = 0;
            int shown = 0;

            foreach (int index in variants)
            {
                DialogueCsvIssue issue = group.items[index];
                string field = review.GetColumnName(issue.column);
                string value = Preview(review.GetCell(issue.row, issue.column));
                string label = $"{GetIssueLocation(issue)} · {field}: {(string.IsNullOrEmpty(value) ? "(empty)" : value)}";

                if (label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                found++;

                if (shown >= 30) continue;

                Button button = new(() =>
                {
                    selectedIssue = index;
                    BuildDetails();
                }) { text = label };
                button.SetEnabled(selectedIssue != index);
                panel.Add(button);
                shown++;
            }

            AddPickerCount(panel, found, shown);
        }

        private static void AddPickerCount(VisualElement panel, int found, int shown)
        {
            string message = found == 0 ? "No matching items." : found > shown ? $"Showing {shown} of {found} results. Use search to narrow the list." : $"{found} result(s).";
            panel.Add(new Label(message));
        }

        private void BuildIssueGroups()
        {
            issueGroups.Clear();
            Dictionary<string, IssueGroup> grouped = new(StringComparer.Ordinal);

            for (int i = 0; i < review.issues.Count; i++)
            {
                DialogueCsvIssue issue = review.issues[i];
                string key = string.IsNullOrEmpty(issue.referenceGroup) ? "issue:" + i : "reference:" + issue.referenceGroup;

                if (!grouped.TryGetValue(key, out IssueGroup group))
                {
                    group = new IssueGroup { key = key, title = issue.title, warning = issue.warning };
                    grouped.Add(key, group);
                    issueGroups.Add(group);
                }

                group.items.Add(issue);
            }
        }

        private string GetIssueLocation(DialogueCsvIssue issue)
        {
            if (issue.row < 0) return "Whole CSV";

            string location = issue.row == 0 ? "Header" : $"Line {issue.line}";

            if (issue.column < 0) return location;

            string field = issue.row == 0 ? $"Column {issue.column + 1}" : review.GetColumnName(issue.column);

            return string.IsNullOrEmpty(field) ? location : $"{location} · {field}";
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

        private sealed class IssueGroup
        {
            public string key;
            public string title;
            public bool warning;
            public readonly List<DialogueCsvIssue> items = new();
        }

        private static string Preview(string text)
        {
            text = text.Replace('\n', ' ').Replace('\r', ' ');

            return text.Length > 100 ? text.Substring(0, 100) + "..." : text;
        }
    }
}
