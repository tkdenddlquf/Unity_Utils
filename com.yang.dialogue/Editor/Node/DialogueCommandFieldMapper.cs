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
        }

        private sealed class Selection
        {
            public MissingField source;
            public PopupField<string> popup;
            public Dictionary<string, FieldOption> choices;
        }

        private sealed class Mapping
        {
            public int sourceId;
            public FieldOption target;
            public bool discard;
            public GenericData.DataType sourceType;
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

        private const string KEEP = "Keep unresolved";
        private const string DISCARD = "Discard stored value";

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
            mapper.titleContent = new GUIContent("Resolve Dialogue IDs");
            mapper.minSize = new Vector2(460, 320);
            mapper.ShowUtility();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            selections.Clear();

            if (editor == null || asset == null || editor.SO != asset)
            {
                root.Add(new HelpBox("Reopen the mapping window from the dialogue node.", HelpBoxMessageType.Info));
                return;
            }

            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.Add(new Label($"Unmatched IDs · {schemaId}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            root.Add(new HelpBox("Saved arguments stay unchanged until you apply a mapping. Only fields with matching types can receive a value.", HelpBoxMessageType.Info));

            ScrollView scroll = new() { style = { flexGrow = 1 } };
            root.Add(scroll);

            if (!TryGetSelectedData(out List<GenericData> data))
            {
                scroll.Add(new HelpBox("The original instruction is no longer available.", HelpBoxMessageType.Warning));
                return;
            }

            HashSet<int> knownIds = new();

            foreach (FieldOption field in fields) knownIds.Add(field.id);

            HashSet<int> shownIds = new();

            for (int i = 1; i + 1 < data.Count; i += 2)
            {
                if (!data[i].TryGetInt(out int oldId) || knownIds.Contains(oldId) || !shownIds.Add(oldId)) continue;

                MissingField missing = new() { id = oldId, value = data[i + 1] };
                List<string> labels = new() { KEEP, DISCARD };
                Dictionary<string, FieldOption> choices = new();

                foreach (FieldOption field in fields)
                {
                    if (field.type != missing.value.Type) continue;

                    string current = TryFind(data, field.id, out GenericData currentValue) ? currentValue.ToString() : "not set";
                    string label = $"{field.name} (ID {field.id}, current: {current})";
                    labels.Add(label);
                    choices.Add(label, field);
                }

                VisualElement group = new();
                group.style.marginBottom = 12;
                group.Add(new Label($"Previous ID {oldId} · {missing.value.Type}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                group.Add(new Label($"Stored value: {missing.value}"));
                PopupField<string> popup = new("Action", labels, 0);
                group.Add(popup);
                scroll.Add(group);
                selections.Add(new Selection { source = missing, popup = popup, choices = choices });
            }

            if (selections.Count == 0)
            {
                scroll.Add(new HelpBox("No unresolved argument IDs remain.", HelpBoxMessageType.Info));
                return;
            }

            applyAll = new Toggle("Apply mappings to other matching instructions in this graph") { value = false };
            applyAll.tooltip = "Matching instruction IDs and old field IDs are converted. Existing non-default target values are never overwritten.";
            root.Add(applyAll);
            root.Add(new HelpBox("If a target already contains a non-default value, the operation is cancelled rather than overwriting it. Discard requires confirmation.", HelpBoxMessageType.None));
            VisualElement actions = new() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            actions.Add(new Button(Close) { text = "Later" });
            actions.Add(new Button(Apply) { text = "Apply selected actions" });
            root.Add(actions);
        }

        private bool TryGetSelectedData(out List<GenericData> data)
        {
            data = null;

            if (editor == null || editor.SO != asset) return false;

            NodeData node = editor.GetNode(nodeGuid);

            if (node.guid != nodeGuid || node.type != nodeType || commandIndex < 0 || commandIndex >= node.EditorOptionDatas.Count) return false;

            List<GenericData> option = node.EditorOptionDatas[commandIndex].data;

            if (option == null || option.Count == 0 || option[0].ToString() != schemaId) return false;

            data = option;
            return true;
        }

        private void Apply()
        {
            if (!TryGetSelectedData(out List<GenericData> selected))
            {
                EditorUtility.DisplayDialog("Instruction changed", "The command or event has changed. Reopen the mapper from the node.", "OK");
                Close();
                return;
            }

            List<Mapping> mappings = new();
            HashSet<int> targetIds = new();
            bool hasDiscard = false;

            foreach (Selection selection in selections)
            {
                string choice = selection.popup.value;

                if (choice == KEEP) continue;

                bool discard = choice == DISCARD;
                FieldOption target = discard ? null : selection.choices[choice];

                if (!discard && !targetIds.Add(target.id))
                {
                    EditorUtility.DisplayDialog("Duplicate target", "Two old IDs cannot map to the same field.", "OK");
                    return;
                }

                mappings.Add(new Mapping { sourceId = selection.source.id, sourceType = selection.source.value.Type, target = target, discard = discard });
                hasDiscard |= discard;
            }

            if (mappings.Count == 0) return;

            foreach (Mapping mapping in mappings)
            {
                if (TryFind(selected, mapping.sourceId, out GenericData current) && current.Type == mapping.sourceType) continue;

                EditorUtility.DisplayDialog("Instruction changed", "The unmatched argument has changed. Reopen the mapper from the node.", "OK");
                Close();
                return;
            }

            if (hasDiscard && !EditorUtility.DisplayDialog("Discard stored arguments?", "Discarded values cannot be recovered except through Undo. Continue?", "Discard and apply", "Cancel")) return;

            List<List<GenericData>> targets = new();

            if (applyAll.value)
            {
                foreach (NodeData node in editor.SO.EditorNodes)
                {
                    if (node.type != nodeType) continue;

                    foreach (DataWrapper wrapper in node.EditorOptionDatas)
                    {
                        List<GenericData> data = wrapper.data;

                        if (data.Count > 0 && data[0].ToString() == schemaId) targets.Add(data);
                    }
                }
            }
            else targets.Add(selected);

            int changes = 0;

            foreach (List<GenericData> data in targets)
            {
                foreach (Mapping mapping in mappings)
                {
                    if (!TryFind(data, mapping.sourceId, out GenericData value)) continue;

                    if (value.Type != mapping.sourceType)
                    {
                        EditorUtility.DisplayDialog("Incompatible stored value", $"ID {mapping.sourceId} has different types across instructions. Nothing was changed.", "OK");
                        return;
                    }

                    if (mapping.discard) continue;

                    if (!TryFind(data, mapping.target.id, out GenericData existing)) continue;

                    if (IsDefaultValue(existing, mapping.target.defaultValue)) continue;

                    EditorUtility.DisplayDialog("Existing target value", $"Field '{mapping.target.name}' already contains a non-default value in another instruction. Nothing was changed. Resolve that instruction separately.", "OK");
                    return;
                }
            }

            Undo.RecordObject(asset, "Resolve Dialogue Argument IDs");

            foreach (List<GenericData> data in targets)
            {
                foreach (Mapping mapping in mappings)
                {
                    if (!TryFind(data, mapping.sourceId, out GenericData value)) continue;

                    Remove(data, mapping.sourceId);

                    if (!mapping.discard) Replace(data, mapping.target.id, value);

                    changes++;
                }
            }

            if (changes == 0) return;

            EditorUtility.SetDirty(asset);
            editor.SetUnsaved();
            Close();
            editor.RefreshAfterArgumentMapping();
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
