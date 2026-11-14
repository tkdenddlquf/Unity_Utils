using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    internal sealed class DialogueCommandFieldMapper : EditorWindow
    {
        internal sealed class FieldOption
        {
            public int id;
            public string name;
            public GenericData.DataType type;
            public GenericData defaultValue;
        }

        internal sealed class MissingField
        {
            public int id;
            public GenericData value;
            public FieldOption incompatibleField;
        }

        private sealed class Selection
        {
            public MissingField source;
            public PopupField<string> popup;
            public readonly Dictionary<string, FieldOption> choices = new();
            public readonly List<string> labels = new();
        }

        private sealed class Mapping
        {
            public int sourceId;
            public GenericData.DataType sourceType;
            public FieldOption incompatibleField;
            public FieldOption target;
            public bool discard;
            public bool reset;
        }

        private sealed class Instruction
        {
            public string nodeGuid;
            public int index;
            public List<GenericData> data;
        }

        private sealed class Change
        {
            public Instruction instruction;
            public Mapping mapping;
            public GenericData previous;
            public bool hasTarget;
            public GenericData previousTarget;
        }

        private sealed class Conflict
        {
            public string nodeGuid;
            public string description;
        }

        private sealed class Plan
        {
            public readonly List<Change> changes = new();
            public readonly List<Conflict> conflicts = new();
            public int matchedInstructions;
            public int changedInstructions;
            public int skippedInstructions;
        }

        private DialogueEditorWindow editor;
        private DialogueSO asset;
        private string nodeGuid;
        private string schemaId;
        private int commandIndex;
        private NodeType nodeType;
        private List<FieldOption> fields;
        private readonly List<Selection> selections = new();
        private Toggle applyAll;
        private Toggle skipConflicts;
        private Label previewSummary;
        private HelpBox previewWarning;
        private Foldout changesPanel;
        private Foldout conflictsPanel;
        private VisualElement changeItems;
        private VisualElement conflictItems;
        private Button applyButton;

        private const string KEEP = "Keep unchanged";
        private const string DISCARD = "Discard old value";
        private const string RESET = "Reset to schema default";

        internal static void Open(DialogueEditorWindow editor, string nodeGuid, int commandIndex, string schemaId, NodeType nodeType, List<FieldOption> fields)
        {
            DialogueCommandFieldMapper mapper = CreateInstance<DialogueCommandFieldMapper>();
            mapper.editor = editor;
            mapper.asset = editor.SO;
            mapper.nodeGuid = nodeGuid;
            mapper.commandIndex = commandIndex;
            mapper.schemaId = schemaId;
            mapper.nodeType = nodeType;
            mapper.fields = fields;
            mapper.titleContent = new GUIContent("Resolve Dialogue Fields");
            mapper.minSize = new Vector2(600, 520);
            mapper.ShowUtility();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            selections.Clear();

            if (!TryGetSelectedData(out List<GenericData> data))
            {
                root.Add(new HelpBox("The instruction has changed. Reopen the mapper from the node.", HelpBoxMessageType.Warning));
                return;
            }

            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.Add(new Label($"Resolve fields · {schemaId}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            root.Add(new HelpBox("Select a destination for each saved value. Suggestions are not applied until you confirm the preview. Unselected values remain untouched.", HelpBoxMessageType.Info));

            ScrollView scroll = new() { style = { flexGrow = 1, marginTop = 8, marginBottom = 8 } };
            root.Add(scroll);
            HashSet<int> issueIds = new();

            for (int i = 1; i + 1 < data.Count; i += 2)
            {
                if (!data[i].TryGetInt(out int oldId)) continue;

                FieldOption existing = FindField(oldId);
                GenericData stored = data[i + 1];

                if (existing != null && existing.type == stored.Type) continue;

                if (!issueIds.Add(oldId)) continue;

                MissingField missing = new() { id = oldId, value = stored, incompatibleField = existing };
                Selection selection = new() { source = missing };
                selection.labels.Add(KEEP);
                selection.labels.Add(existing == null ? DISCARD : RESET);
                selections.Add(selection);
            }

            if (selections.Count == 0)
            {
                scroll.Add(new HelpBox("No unresolved IDs or incompatible values remain.", HelpBoxMessageType.Info));
                return;
            }

            foreach (Selection selection in selections)
            {
                foreach (FieldOption field in fields)
                {
                    if (field.type != selection.source.value.Type || issueIds.Contains(field.id)) continue;

                    string current = TryFind(data, field.id, out GenericData currentValue) ? Short(currentValue.ToString()) : "<unset>";
                    string label = $"{field.name} · ID {field.id} · current: {current}";
                    selection.choices.Add(label, field);
                    selection.labels.Add(label);
                }
            }

            Dictionary<int, int> suggestionCounts = new();
            Dictionary<Selection, string> suggestions = new();

            foreach (Selection selection in selections)
            {
                string suggestedLabel = null;
                int available = 0;

                foreach (KeyValuePair<string, FieldOption> pair in selection.choices)
                {
                    if (TryFind(data, pair.Value.id, out GenericData stored) && !IsDefaultValue(stored, pair.Value.defaultValue)) continue;

                    available++;
                    suggestedLabel = pair.Key;
                }

                if (available != 1) continue;

                suggestions.Add(selection, suggestedLabel);
                int targetId = selection.choices[suggestedLabel].id;
                suggestionCounts[targetId] = suggestionCounts.TryGetValue(targetId, out int count) ? count + 1 : 1;
            }

            foreach (Selection selection in selections)
            {
                MissingField missing = selection.source;
                VisualElement group = new() { style = { marginBottom = 12, paddingBottom = 10, borderBottomWidth = 1, borderBottomColor = new Color(0.45f, 0.45f, 0.45f, 0.35f) } };
                string sourceLabel = missing.incompatibleField == null ? $"Unmatched field · ID {missing.id}" : $"Incompatible field · {missing.incompatibleField.name} (ID {missing.id})";
                group.Add(new Label(sourceLabel) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                group.Add(new Label($"Saved: {Short(missing.value.ToString())}  ·  {missing.value.Type}") { tooltip = missing.value.ToString() });

                if (missing.incompatibleField != null) group.Add(new Label($"Expected: {missing.incompatibleField.type}  ·  default {missing.incompatibleField.defaultValue}"));

                string suggested = null;

                if (suggestions.TryGetValue(selection, out string label) && suggestionCounts[selection.choices[label].id] == 1) suggested = label;

                int selected = suggested == null ? 0 : selection.labels.IndexOf(suggested);
                selection.popup = new PopupField<string>("Action", selection.labels, selected);
                selection.popup.RegisterValueChangedCallback(_ => OnSelectionChanged());
                group.Add(selection.popup);

                if (suggested != null) group.Add(new Label($"Suggested: {suggested} (only compatible empty/default destination)"));
                else if (selection.choices.Count == 0 && missing.incompatibleField == null) group.Add(new HelpBox("There are no compatible fields. Keep this value or discard it.", HelpBoxMessageType.Warning));

                scroll.Add(group);
            }

            RefreshAvailableChoices();
            applyAll = new Toggle("Apply to matching Command/Event instructions in this graph") { value = false };
            applyAll.tooltip = "Only instructions with the same type and schema ID are considered. The preview shows every affected node.";
            applyAll.RegisterValueChangedCallback(_ => RefreshPreview());
            root.Add(applyAll);
            skipConflicts = new Toggle("Skip conflicting instructions") { value = false };
            skipConflicts.tooltip = "Conflicting instructions remain unchanged. Other instructions can be updated together.";
            skipConflicts.RegisterValueChangedCallback(_ => RefreshPreview());
            root.Add(skipConflicts);

            previewSummary = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } };
            root.Add(previewSummary);
            previewWarning = new HelpBox("", HelpBoxMessageType.Warning);
            root.Add(previewWarning);
            ScrollView previewScroll = new() { style = { maxHeight = 210, marginTop = 5, marginBottom = 6 } };
            root.Add(previewScroll);
            changesPanel = new Foldout { text = "Planned changes", value = true };
            conflictsPanel = new Foldout { text = "Conflicts", value = true };
            changeItems = new VisualElement();
            conflictItems = new VisualElement();
            changesPanel.Add(changeItems);
            conflictsPanel.Add(conflictItems);
            previewScroll.Add(changesPanel);
            previewScroll.Add(conflictsPanel);

            VisualElement buttons = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            buttons.Add(new Button(Close) { text = "Cancel" });
            applyButton = new Button(Apply) { text = "Review and apply" };
            buttons.Add(applyButton);
            root.Add(buttons);
            RefreshPreview();
        }

        private void OnSelectionChanged()
        {
            RefreshAvailableChoices();
            RefreshPreview();
        }

        private void RefreshAvailableChoices()
        {
            foreach (Selection selection in selections)
            {
                string current = selection.popup.value;
                HashSet<int> occupied = new();

                foreach (Selection other in selections)
                {
                    if (other == selection || !other.choices.TryGetValue(other.popup.value, out FieldOption target)) continue;

                    occupied.Add(target.id);
                }

                List<string> allowed = new() { KEEP, selection.source.incompatibleField == null ? DISCARD : RESET };

                foreach (string label in selection.labels)
                {
                    if (!selection.choices.TryGetValue(label, out FieldOption field) || occupied.Contains(field.id)) continue;

                    allowed.Add(label);
                }

                selection.popup.choices = allowed;

                if (allowed.Contains(current)) selection.popup.SetValueWithoutNotify(current);
                else selection.popup.SetValueWithoutNotify(KEEP);
            }
        }

        private FieldOption FindField(int id)
        {
            foreach (FieldOption field in fields)
            {
                if (field.id == id) return field;
            }

            return null;
        }

        private bool TryGetSelectedData(out List<GenericData> data)
        {
            data = null;

            if (editor == null || asset == null || editor.SO != asset) return false;

            NodeData node = editor.GetNode(nodeGuid);

            if (node.guid != nodeGuid || node.type != nodeType || commandIndex < 0 || commandIndex >= node.EditorOptionDatas.Count) return false;

            List<GenericData> option = node.EditorOptionDatas[commandIndex].data;

            if (option == null || option.Count == 0 || option[0].ToString() != schemaId) return false;

            data = option;
            return true;
        }

        private List<Mapping> GetMappings()
        {
            List<Mapping> mappings = new();

            foreach (Selection selection in selections)
            {
                string choice = selection.popup.value;

                if (choice == KEEP) continue;

                mappings.Add(new Mapping
                {
                    sourceId = selection.source.id,
                    sourceType = selection.source.value.Type,
                    incompatibleField = selection.source.incompatibleField,
                    target = selection.choices.TryGetValue(choice, out FieldOption target) ? target : null,
                    discard = choice == DISCARD,
                    reset = choice == RESET
                });
            }

            return mappings;
        }

        private List<Instruction> GetInstructions()
        {
            List<Instruction> result = new();

            if (applyAll == null || !applyAll.value)
            {
                if (TryGetSelectedData(out List<GenericData> data)) result.Add(new Instruction { nodeGuid = nodeGuid, index = commandIndex, data = data });

                return result;
            }

            foreach (NodeData node in asset.EditorNodes)
            {
                if (node.type != nodeType) continue;

                for (int i = 0; i < node.EditorOptionDatas.Count; i++)
                {
                    List<GenericData> data = node.EditorOptionDatas[i].data;

                    if (data == null || data.Count == 0 || data[0].ToString() != schemaId) continue;

                    result.Add(new Instruction { nodeGuid = node.guid, index = i, data = data });
                }
            }

            return result;
        }

        private Plan BuildPlan(List<Mapping> mappings)
        {
            Plan plan = new();

            foreach (Instruction instruction in GetInstructions())
            {
                plan.matchedInstructions++;
                List<Change> pending = new();
                List<string> problems = new();

                foreach (Mapping mapping in mappings)
                {
                    if (!TryFind(instruction.data, mapping.sourceId, out GenericData source)) continue;

                    if (source.Type != mapping.sourceType)
                    {
                        if (mapping.incompatibleField != null && source.Type == mapping.incompatibleField.type) continue;

                        problems.Add($"ID {mapping.sourceId}: expected saved type {mapping.sourceType}, found {source.Type}");
                        continue;
                    }

                    bool hasTarget = false;
                    GenericData previousTarget = default;

                    if (mapping.target != null)
                    {
                        hasTarget = TryFind(instruction.data, mapping.target.id, out previousTarget);

                        if (hasTarget && !IsDefaultValue(previousTarget, mapping.target.defaultValue) && !IsDefaultValue(previousTarget, source))
                        {
                            problems.Add($"{mapping.target.name} (ID {mapping.target.id}) already contains '{previousTarget}'");
                            continue;
                        }
                    }

                    pending.Add(new Change { instruction = instruction, mapping = mapping, previous = source, hasTarget = hasTarget, previousTarget = previousTarget });
                }

                if (problems.Count > 0)
                {
                    plan.skippedInstructions++;

                    foreach (string problem in problems) plan.conflicts.Add(new Conflict { nodeGuid = instruction.nodeGuid, description = $"Instruction {instruction.index + 1}: {problem}" });

                    continue;
                }

                if (pending.Count == 0) continue;

                plan.changedInstructions++;
                plan.changes.AddRange(pending);
            }

            return plan;
        }

        private void RefreshPreview()
        {
            if (previewSummary == null) return;

            List<Mapping> mappings = GetMappings();
            Plan plan = BuildPlan(mappings);
            previewSummary.text = $"{plan.changedInstructions}/{plan.matchedInstructions} matching instructions · {plan.changes.Count} changes · {plan.skippedInstructions} conflicting instructions";
            changeItems.Clear();
            changesPanel.text = $"Planned changes ({plan.changes.Count})";

            foreach (Change change in plan.changes)
            {
                Mapping mapping = change.mapping;
                string location = $"{change.instruction.nodeGuid} / instruction {change.instruction.index + 1}";
                string action = mapping.reset
                    ? $"ID {mapping.sourceId}: {change.previous} → default {mapping.incompatibleField.defaultValue}"
                    : mapping.discard
                        ? $"ID {mapping.sourceId}: discard {change.previous}"
                        : $"ID {mapping.sourceId} → {mapping.target.name} (ID {mapping.target.id}): {(change.hasTarget ? change.previousTarget.ToString() : "<unset>")} → {change.previous}";

                if (mapping.incompatibleField != null && !mapping.reset) action += $"; reset {mapping.incompatibleField.name} to {mapping.incompatibleField.defaultValue}";

                changeItems.Add(new Label($"{location}  |  {action}") { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 3 } });
            }

            conflictItems.Clear();
            conflictsPanel.text = $"Conflicts ({plan.conflicts.Count})";

            foreach (Conflict conflict in plan.conflicts)
            {
                VisualElement row = new() { style = { flexDirection = FlexDirection.Row, marginBottom = 4, alignItems = Align.Center } };
                Label description = new($"{conflict.nodeGuid} · {conflict.description}") { style = { flexGrow = 1, whiteSpace = WhiteSpace.Normal } };
                Button locate = new(() =>
                {
                    editor.FocusNode(conflict.nodeGuid, true);
                    editor.Focus();
                }) { text = "Find node" };
                row.Add(description);
                row.Add(locate);
                conflictItems.Add(row);
            }

            bool blocked = plan.conflicts.Count > 0 && !skipConflicts.value;
            previewWarning.style.display = plan.conflicts.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            previewWarning.text = blocked
                ? "Existing values would be overwritten or stored types differ. Resolve the listed instructions, or explicitly enable 'Skip conflicting instructions'."
                : plan.conflicts.Count > 0 ? "Conflicting instructions will be left unchanged; only the listed planned changes will be applied." : "";
            applyButton.SetEnabled(plan.changes.Count > 0 && !blocked);
        }

        private void Apply()
        {
            if (!TryGetSelectedData(out List<GenericData> selected))
            {
                EditorUtility.DisplayDialog("Instruction changed", "Reopen the mapper from the node.", "OK");
                Close();
                return;
            }

            foreach (Selection selection in selections)
            {
                if (TryFind(selected, selection.source.id, out GenericData stored) && stored.Type == selection.source.value.Type && stored.ToString() == selection.source.value.ToString()) continue;

                EditorUtility.DisplayDialog("Instruction changed", "Saved values have changed since opening this window. Reopen the mapper.", "OK");
                Close();
                return;
            }

            List<Mapping> mappings = GetMappings();
            Plan plan = BuildPlan(mappings);

            if (plan.changes.Count == 0 || plan.conflicts.Count > 0 && !skipConflicts.value) return;

            int discarded = 0;

            foreach (Change change in plan.changes)
            {
                if (change.mapping.discard || change.mapping.reset) discarded++;
            }

            string summary = $"Change {plan.changes.Count} stored values in {plan.changedInstructions} instructions?";

            if (plan.skippedInstructions > 0) summary += $"\n\n{plan.skippedInstructions} conflicting instructions will remain unchanged.";

            if (discarded > 0) summary += $"\n\n{discarded} stored values will be discarded or reset to defaults. Undo is available.";

            if (!EditorUtility.DisplayDialog("Confirm field mappings", summary, "Apply", "Cancel")) return;

            Undo.RecordObject(asset, "Resolve Dialogue Argument IDs");

            foreach (Change change in plan.changes)
            {
                Mapping mapping = change.mapping;
                List<GenericData> data = change.instruction.data;
                Remove(data, mapping.sourceId);

                if (mapping.incompatibleField != null) Replace(data, mapping.sourceId, mapping.incompatibleField.defaultValue);

                if (mapping.target != null) Replace(data, mapping.target.id, change.previous);
            }

            EditorUtility.SetDirty(asset);
            editor.SetUnsaved();
            Close();
            editor.RefreshAfterArgumentMapping();
        }

        private static string Short(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";

            string result = value.Replace("\r", " ").Replace("\n", " ");

            return result.Length > 45 ? result.Substring(0, 45) + "…" : result;
        }

        private static bool TryFind(IReadOnlyList<GenericData> data, int id, out GenericData value)
        {
            for (int i = 1; i + 1 < data.Count; i += 2)
            {
                if (!data[i].TryGetInt(out int storedId) || storedId != id) continue;

                value = data[i + 1];
                return true;
            }

            value = default;
            return false;
        }

        private static bool IsDefaultValue(GenericData stored, GenericData expected)
        {
            if (stored.Type != expected.Type) return false;

            return stored.Type switch
            {
                GenericData.DataType.Int => stored.GetInt() == expected.GetInt(),
                GenericData.DataType.Float => stored.GetFloat().Equals(expected.GetFloat()),
                GenericData.DataType.Long => stored.GetLong() == expected.GetLong(),
                GenericData.DataType.Bool => stored.GetBool() == expected.GetBool(),
                GenericData.DataType.Color => stored.GetColor().Equals(expected.GetColor()),
                GenericData.DataType.Guid => stored.ToString() == expected.ToString(),
                GenericData.DataType.Enum => stored.TryGetEnumInt(out int oldEnum) && expected.TryGetEnumInt(out int newEnum) && oldEnum == newEnum,
                GenericData.DataType.String => stored.GetString() == expected.GetString(),
                _ => false
            };
        }

        private static void Remove(List<GenericData> data, int id)
        {
            for (int i = data.Count - 2; i >= 1; i -= 2)
            {
                if (!data[i].TryGetInt(out int storedId) || storedId != id) continue;

                data.RemoveAt(i + 1);
                data.RemoveAt(i);
            }
        }

        private static void Replace(List<GenericData> data, int id, GenericData value)
        {
            for (int i = 1; i + 1 < data.Count; i += 2)
            {
                if (!data[i].TryGetInt(out int storedId) || storedId != id) continue;

                data[i + 1] = value;
                return;
            }

            data.Add(new GenericData(id));
            data.Add(value);
        }
    }
}
