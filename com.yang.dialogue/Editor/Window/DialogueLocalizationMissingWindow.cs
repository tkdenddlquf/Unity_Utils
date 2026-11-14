using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    internal sealed class DialogueLocalizationMissingWindow : EditorWindow
    {
        private sealed class Reference
        {
            public string nodeGuid;
            public NodeType nodeType;
            public bool speaker;
            public bool tableMissing;
            public bool portEntry;
            public int tableIndex;
            public int entryIndex;
            public string tableName;
            public Guid tableGuid;
            public string entryKey;
            public long entryId;
        }

        private sealed class Group
        {
            public Reference first;
            public readonly List<Reference> references = new();
            public PopupField<string> selection;
            public readonly Dictionary<string, Guid> tableTargets = new();
            public readonly Dictionary<string, long> entryTargets = new();
        }

        private DialogueEditorWindow editor;
        private DialogueSO asset;
        private readonly List<Group> groups = new();
        private Label summary;
        private ScrollView scroll;

        private const string KEEP = "Keep unresolved";

        internal static void Open(DialogueEditorWindow editor)
        {
            DialogueLocalizationMissingWindow window = CreateInstance<DialogueLocalizationMissingWindow>();
            window.editor = editor;
            window.asset = editor.SO;
            window.titleContent = new GUIContent("Resolve Localization");
            window.minSize = new Vector2(620, 420);
            window.Show();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;

            if (editor == null || asset == null || editor.SO != asset)
            {
                root.Add(new HelpBox("Reopen this window from the Dialogue Editor's Refresh toolbar.", HelpBoxMessageType.Info));
                return;
            }

            Label title = new("Resolve Missing Localization") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 16 } };
            root.Add(title);
            root.Add(new HelpBox("Only unresolved references are listed. Choose an existing table or entry to replace the stored reference. Keeping an item leaves its data unchanged. References with the same source are grouped together.", HelpBoxMessageType.Info));

            summary = new Label();
            summary.style.marginTop = 8;
            summary.style.marginBottom = 6;
            root.Add(summary);

            scroll = new ScrollView() { style = { flexGrow = 1 } };
            root.Add(scroll);

            VisualElement actions = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 8 } };
            actions.Add(new Button(Rescan) { text = "Rescan" });
            actions.Add(new Button(Close) { text = "Later" });
            actions.Add(new Button(Apply) { text = "Apply selected mappings" });
            root.Add(actions);

            Rescan();
        }

        private void Rescan()
        {
            if (editor == null || editor.SO != asset)
            {
                Close();
                return;
            }

            IReadOnlyList<LocalizationTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
            List<Reference> issues = Collect(asset, collections);
            BuildGroups(issues);
            scroll.Clear();

            if (issues.Count == 0)
            {
                summary.text = "No missing localization references.";
                scroll.Add(new HelpBox("All existing Dialogue and Choice localization references can be resolved.", HelpBoxMessageType.Info));
                return;
            }

            summary.text = $"{issues.Count} unresolved references · {groups.Count} groups";

            foreach (Group group in groups) BuildGroup(group, collections);
        }

        private void BuildGroup(Group group, IReadOnlyList<LocalizationTableCollection> collections)
        {
            Reference issue = group.first;
            VisualElement section = new();
            section.style.marginBottom = 10;
            section.style.paddingLeft = 8;
            section.style.paddingRight = 8;
            section.style.paddingTop = 8;
            section.style.paddingBottom = 8;
            section.style.borderBottomWidth = 1;
            section.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);

            bool mixedRoles = false;

            foreach (Reference reference in group.references)
            {
                if (reference.speaker != issue.speaker) mixedRoles = true;
            }

            string role = mixedRoles ? "Speaker / Text" : issue.speaker ? "Speaker" : "Text";
            string kind = issue.tableMissing ? "Missing table" : "Missing entry";
            section.Add(new Label($"{kind} · {role} · {group.references.Count} reference(s)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            section.Add(new Label($"Stored table: {issue.tableName} · GUID: {issue.tableGuid}"));

            if (!issue.tableMissing) section.Add(new Label($"Stored entry: {issue.entryKey} · ID: {issue.entryId}"));
            else if (issue.entryId != 0 || !string.IsNullOrEmpty(issue.entryKey)) section.Add(new Label($"Example stored entry: {issue.entryKey} · ID: {issue.entryId}"));

            List<string> options = new() { KEEP };

            if (issue.tableMissing)
            {
                foreach (LocalizationTableCollection collection in collections)
                {
                    Guid guid = collection.TableCollectionNameReference.TableCollectionNameGuid;
                    string label = $"{collection.TableCollectionName} ({guid})";
                    options.Add(label);
                    group.tableTargets.Add(label, guid);
                }
            }
            else
            {
                LocalizationTableCollection collection = collections.ResolveCollection(issue.tableName, issue.tableGuid);

                if (collection != null)
                {
                    foreach (SharedTableData.SharedTableEntry entry in collection.SharedData.Entries)
                    {
                        string label = $"{entry.Key} (ID {entry.Id})";
                        options.Add(label);
                        group.entryTargets.Add(label, entry.Id);
                    }
                }
            }

            group.selection = new PopupField<string>("Replace with", options, 0);
            section.Add(group.selection);

            VisualElement controls = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            controls.Add(new Button(() =>
            {
                editor.Focus();
                editor.FocusNode(issue.nodeGuid, true);
            }) { text = $"Go to {issue.nodeType} node" });
            section.Add(controls);
            scroll.Add(section);
        }

        private void Apply()
        {
            if (editor == null || editor.SO != asset)
            {
                EditorUtility.DisplayDialog("Dialogue changed", "Reopen the resolver from the active Dialogue asset.", "OK");
                Close();
                return;
            }

            IReadOnlyList<LocalizationTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
            List<Reference> latest = Collect(asset, collections);
            Dictionary<(bool, bool, Guid, string, long, string), List<Reference>> currentGroups = new();

            foreach (Reference reference in latest)
            {
                (bool, bool, Guid, string, long, string) key = Identity(reference);

                if (!currentGroups.TryGetValue(key, out List<Reference> matching))
                {
                    matching = new List<Reference>();
                    currentGroups.Add(key, matching);
                }

                matching.Add(reference);
            }

            foreach (Group group in groups)
            {
                if (group.selection == null || group.selection.value == KEEP) continue;

                (bool, bool, Guid, string, long, string) key = Identity(group.first);

                bool stale = !currentGroups.TryGetValue(key, out List<Reference> matching) || matching.Count != group.references.Count;

                if (!stale)
                {
                    HashSet<(string, int, int, bool)> locations = new();

                    foreach (Reference reference in matching) locations.Add(Location(reference));

                    foreach (Reference reference in group.references)
                    {
                        if (locations.Contains(Location(reference))) continue;

                        stale = true;
                        break;
                    }
                }

                if (stale)
                {
                    EditorUtility.DisplayDialog("References changed", "The stored references changed since this window was opened. Rescan before applying.", "OK");
                    Rescan();
                    return;
                }

                if (group.first.tableMissing)
                {
                    Guid targetGuid = group.tableTargets[group.selection.value];

                    if (FindCollection(collections, targetGuid) != null) continue;
                }
                else
                {
                    LocalizationTableCollection collection = collections.ResolveCollection(group.first.tableName, group.first.tableGuid);
                    long targetId = group.entryTargets[group.selection.value];

                    if (collection != null && collection.SharedData.GetEntry(targetId) != null) continue;
                }

                EditorUtility.DisplayDialog("Localization changed", "A selected table or entry no longer exists. Rescan before applying.", "OK");
                Rescan();
                return;
            }

            int changes = 0;
            bool recorded = false;
            int undoGroup = -1;

            foreach (Group group in groups)
            {
                if (group.selection == null || group.selection.value == KEEP) continue;

                List<Reference> matching = currentGroups[Identity(group.first)];

                foreach (Reference reference in matching)
                {
                    if (!recorded)
                    {
                        Undo.IncrementCurrentGroup();
                        undoGroup = Undo.GetCurrentGroup();
                        Undo.SetCurrentGroupName("Resolve Dialogue Localization References");
                        Undo.RecordObject(asset, "Resolve Dialogue Localization References");
                        recorded = true;
                    }

                    if (group.first.tableMissing)
                    {
                        Guid targetGuid = group.tableTargets[group.selection.value];
                        LocalizationTableCollection target = FindCollection(collections, targetGuid);
                        ReplaceTable(reference, target);
                    }
                    else
                    {
                        LocalizationTableCollection collection = collections.ResolveCollection(reference.tableName, reference.tableGuid);
                        long targetId = group.entryTargets[group.selection.value];
                        SharedTableData.SharedTableEntry target = collection.SharedData.GetEntry(targetId);
                        ReplaceEntry(reference, target);
                    }

                    changes++;
                }
            }

            if (changes == 0) return;

            EditorUtility.SetDirty(asset);
            editor.SetUnsaved();
            editor.RefreshAfterLocalizationMapping();
            Undo.CollapseUndoOperations(undoGroup);
            Rescan();
        }

        private void ReplaceTable(Reference reference, LocalizationTableCollection collection)
        {
            NodeData node = editor.GetNode(reference.nodeGuid);
            List<GenericData> data = new(node.EditorOptionDatas[reference.tableIndex].data);
            data[0] = new GenericData(collection.TableCollectionName);
            data[1] = new GenericData(collection.TableCollectionNameReference.TableCollectionNameGuid);
            node.EditorOptionDatas[reference.tableIndex] = new DataWrapper(data);
        }

        private void ReplaceEntry(Reference reference, SharedTableData.SharedTableEntry target)
        {
            NodeData node = editor.GetNode(reference.nodeGuid);
            List<DataWrapper> wrappers = reference.portEntry ? node.EditorPortDatas : node.EditorOptionDatas;
            List<GenericData> data = new(wrappers[reference.entryIndex].data);
            data[0] = new GenericData(target.Key);
            data[1] = new GenericData(target.Id);
            wrappers[reference.entryIndex] = new DataWrapper(data);
        }

        private void BuildGroups(List<Reference> issues)
        {
            groups.Clear();
            Dictionary<(bool, bool, Guid, string, long, string), Group> index = new();

            foreach (Reference issue in issues)
            {
                (bool, bool, Guid, string, long, string) key = Identity(issue);

                if (!index.TryGetValue(key, out Group group))
                {
                    group = new Group { first = issue };
                    index.Add(key, group);
                    groups.Add(group);
                }

                group.references.Add(issue);
            }
        }

        private static (string, int, int, bool) Location(Reference reference)
        {
            return (reference.nodeGuid, reference.tableIndex, reference.entryIndex, reference.portEntry);
        }

        private static (bool, bool, Guid, string, long, string) Identity(Reference reference)
        {
            bool noTableIdentity = reference.tableGuid == Guid.Empty && string.IsNullOrEmpty(reference.tableName);
            bool includeEntry = !reference.tableMissing || noTableIdentity;
            string tableName = reference.tableGuid == Guid.Empty ? reference.tableName : "";
            long entryId = includeEntry ? reference.entryId : 0;
            string entryKey = includeEntry && entryId == 0 ? reference.entryKey : "";

            return (reference.tableMissing, noTableIdentity && reference.speaker, reference.tableGuid, tableName, entryId, entryKey);
        }

        private static LocalizationTableCollection FindCollection(IReadOnlyList<LocalizationTableCollection> collections, Guid guid)
        {
            foreach (LocalizationTableCollection collection in collections)
            {
                if (collection.TableCollectionNameReference.TableCollectionNameGuid == guid) return collection;
            }

            return null;
        }

        private static List<Reference> Collect(DialogueSO asset, IReadOnlyList<LocalizationTableCollection> collections)
        {
            List<Reference> result = new();

            foreach (NodeData node in asset.EditorNodes)
            {
                List<DataWrapper> options = node.EditorOptionDatas;

                if (node.type == NodeType.Dialogue && options.Count >= 4)
                {
                    CollectGroup(result, collections, node, 0, options, 1, 1, false, true);
                    CollectGroup(result, collections, node, 2, options, 3, 1, false, false);
                }
                else if (node.type == NodeType.Choice && options.Count >= 3)
                {
                    CollectGroup(result, collections, node, 0, options, 1, 1, false, true);
                    CollectGroup(result, collections, node, 2, node.EditorPortDatas, 0, node.EditorPortDatas.Count, true, false);
                }
            }

            return result;
        }

        private static void CollectGroup(List<Reference> result, IReadOnlyList<LocalizationTableCollection> collections, NodeData node, int tableIndex, List<DataWrapper> entries, int start, int count, bool portEntry, bool speaker)
        {
            List<GenericData> table = node.EditorOptionDatas[tableIndex].data;
            string tableName = table[0].ToString();
            Guid tableGuid = table[1].GetGuid();
            LocalizationTableCollection collection = collections.ResolveCollection(tableName, tableGuid);
            bool hasEntry = false;
            string sampleKey = "";
            long sampleId = 0;

            if (collection == null)
            {
                for (int i = start; i < start + count; i++)
                {
                    List<GenericData> data = entries[i].data;
                    string key = data[0].ToString();
                    long id = data[1].GetLong();
                    hasEntry |= id != 0 || !string.IsNullOrEmpty(key);

                    if (sampleId == 0 && string.IsNullOrEmpty(sampleKey) && (id != 0 || !string.IsNullOrEmpty(key)))
                    {
                        sampleId = id;
                        sampleKey = key;
                    }

                    collection = collections.ResolveCollection(null, Guid.Empty, id, key);

                    if (collection != null) break;
                }
            }

            if (collection == null)
            {
                if (tableGuid == Guid.Empty && string.IsNullOrEmpty(tableName) && !hasEntry) return;

                result.Add(new Reference
                {
                    nodeGuid = node.guid,
                    nodeType = node.type,
                    speaker = speaker,
                    tableMissing = true,
                    tableIndex = tableIndex,
                    tableName = tableName,
                    tableGuid = tableGuid,
                    entryKey = sampleKey,
                    entryId = sampleId
                });
                return;
            }

            for (int i = start; i < start + count; i++)
            {
                List<GenericData> data = entries[i].data;
                string key = data[0].ToString();
                long id = data[1].GetLong();

                if (id == 0 && string.IsNullOrEmpty(key)) continue;

                if (collection.ResolveEntry(id, key) != null) continue;

                result.Add(new Reference
                {
                    nodeGuid = node.guid,
                    nodeType = node.type,
                    speaker = speaker,
                    tableMissing = false,
                    portEntry = portEntry,
                    tableIndex = tableIndex,
                    entryIndex = i,
                    tableName = tableName,
                    tableGuid = tableGuid,
                    entryKey = key,
                    entryId = id
                });
            }
        }
    }
}
