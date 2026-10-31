using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class ChoiceNode : BaseNode
    {
        // optionDatas: [0] speaker table, [1] speaker entry, [2] text table, [3] message. portDatas: key, long ID, hide, then condition triples.
        private readonly List<string> tables;
        private readonly List<System.Guid> tableIds = new();
        private readonly List<EntryData> speakerEntries = new();
        private readonly List<EntryData> textEntries = new();

        private readonly List<string> conditions = new();

        private readonly IReadOnlyList<LocalizationTableCollection> collections;

        private readonly VisualElement textsElement = new();

        public ChoiceNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            tables = window.Tables;
            collections = window.collections;
            collections.SetTableIds(tableIds);
        }

        public override void SetPorts()
        {
            Button add = new(CreateTextEntry) { text = "+", tooltip = "Add choice" };
            add.style.width = 22;
            add.style.height = 20;
            titleButtonContainer.Add(add);
            SetDefault();
            CreateInputPort();
            contentContainer[0].Insert(2, textsElement);
            AddTextField("Speaker", "Speaker Text");
            SetOptions();
            RefreshPreviewEditability();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;
            DropdownMenu menu = evt.menu;
            menu.AppendAction("Add Choice Port", _ => CreateTextEntry());
            menu.AppendSeparator();
        }

        private void SetDefault()
        {
            if (optionDatas.Count == 0)
            {
                DialogueSO so = window.SO;
                LocalizedStringTable speakerOverride = so.SpeakerTable;
                LocalizedStringTable textOverride = so.TextTable;
                DataWrapper speakerTable;
                DataWrapper textTable;

                if (speakerOverride == null || speakerOverride.IsEmpty)
                {
                    speakerTable = new(new(GenericData.DataType.String), new(GenericData.DataType.Guid));
                }
                else
                {
                    TableReference reference = speakerOverride.TableReference;
                    System.Guid guid = reference.TableCollectionNameGuid;
                    if (guid == System.Guid.Empty)
                        guid = collections.ResolveCollection(reference.TableCollectionName, guid)?.TableCollectionNameReference.TableCollectionNameGuid ?? guid;
                    speakerTable = new(new(reference.TableCollectionName), new(guid));
                }

                if (textOverride == null || textOverride.IsEmpty)
                {
                    textTable = new(new(GenericData.DataType.String), new(GenericData.DataType.Guid));
                }
                else
                {
                    TableReference reference = textOverride.TableReference;
                    System.Guid guid = reference.TableCollectionNameGuid;
                    if (guid == System.Guid.Empty)
                        guid = collections.ResolveCollection(reference.TableCollectionName, guid)?.TableCollectionNameReference.TableCollectionNameGuid ?? guid;
                    textTable = new(new(reference.TableCollectionName), new(guid));
                }

                DataWrapper speakerEntry = new(new(GenericData.DataType.String), new(GenericData.DataType.Long));
                DataWrapper message = new(new GenericData(GenericData.DataType.String));
                optionDatas.Add(speakerTable);
                optionDatas.Add(speakerEntry);
                optionDatas.Add(textTable);
                optionDatas.Add(message);
            }

            if (portDatas.Count == 0)
                portDatas.Add(new(new(GenericData.DataType.String), new(GenericData.DataType.Long), new(GenericData.DataType.Bool)));
        }

        private void SetOptions()
        {
            conditions.Clear();
            AddTableField(true);
            AddSpeakerEntryField();
            AddTableField(false);
            AddMessageField();

            for (int i = 0; i < portDatas.Count; i++)
            {
                IReadOnlyList<GenericData> portOptions = portDatas[i].data;
                VisualElement itemContainer = AddTextEntryField(portDatas[i].data);
                Toggle hide = itemContainer.FindParent<Port>().Q<Toggle>("Hide");
                if (portOptions.Count > 3)
                    hide.style.display = DisplayStyle.Flex;
                else hide.style.display = DisplayStyle.None;

                for (int j = 3; j < portOptions.Count; j += 3)
                {
                    int fieldId = portOptions[j].GetInt();

                    switch (portOptions[j + 1].Type)
                    {
                        case GenericData.DataType.Float:
                            {
                                float value = portOptions[j + 1].GetFloat();
                                ValueCheckType type = portOptions[j + 2].GetEnum<ValueCheckType>();
                                itemContainer.Add(GetConditionFloatField(fieldId, value, type));
                            }
                            break;
                        case GenericData.DataType.Bool:
                            {
                                bool value = portOptions[j + 1].GetBool();
                                itemContainer.Add(GetConditionBoolField(fieldId, value));
                            }
                            break;
                    }
                }
            }
        }

        private TextField AddTextField(string title, string name)
        {
            TextField field = new(title)
            {
                name = name,
                isReadOnly = true,
            };
            field.isDelayed = true;
            field.tooltip = "Edit the selected locale's String Table entry (changes commit on Enter or focus loss).";
            field.RegisterValueChangedCallback(evt => OnPreviewChanged(field, evt.newValue));
            field.AddToClassList("dlg-preview");
            textsElement.Add(field);
            return field;
        }

        private void OnPreviewChanged(TextField field, string value)
        {
            bool speaker = field.name == "Speaker Text";
            int index = textsElement.IndexOf(field) - 1;
            if (!speaker && (index < 0 || index >= portDatas.Count)) return;
            List<GenericData> table = optionDatas[speaker ? 0 : 2].data;
            List<GenericData> entry = speaker ? optionDatas[1].data : portDatas[index].data;

            if (!window.EditLocalizedText(table, entry, value))
            {
                field.SetValueWithoutNotify(window.ReadLocalizedText(table, entry));
                RefreshPreview(field);
            }
        }

        private void RefreshPreview(TextField field)
        {
            bool speaker = field.name == "Speaker Text";
            int index = textsElement.IndexOf(field) - 1;
            if (!speaker && (index < 0 || index >= portDatas.Count)) return;
            List<GenericData> tableData = optionDatas[speaker ? 0 : 2].data;
            List<GenericData> entryData = speaker ? optionDatas[1].data : portDatas[index].data;
            field.isReadOnly = !window.CanEditLocalizedText(tableData, entryData);
        }

        private void RefreshPreviewEditability()
        {
            for (int i = 0; i < textsElement.childCount; i++)
            {
                if (textsElement[i] is TextField field)
                    RefreshPreview(field);
            }
        }

        private void SwapPort(int a, int b)
        {
            DialogueSO so = window.SO;
            Undo.RecordObject(so, "Reorder Choice Port");
            (portDatas[a], portDatas[b]) = (portDatas[b], portDatas[a]);
            textsElement.Insert(a + 1, textsElement[b + 1]);
            outputContainer.Insert(a, outputContainer[b]);
            SwapPortLinks(a, b);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private static List<long> GetEntryIds(List<EntryData> entries)
        {
            List<long> ids = new() { 0 };
            foreach (EntryData entry in entries)
                ids.Add(entry.id);
            return ids;
        }

        private static int FindEntryIndex(List<EntryData> entries, List<GenericData> data)
        {
            long id = data[1].GetLong();
            int index = id != 0 ? entries.FindIndex(entry => entry.id == id) : -1;
            return index >= 0 ? index : entries.FindIndex(entry => entry.key == data[0].ToString());
        }

        private string FormatEntry(bool speaker, long id)
        {
            if (id == 0) return "";
            EntryData entry = (speaker ? speakerEntries : textEntries).Find(value => value.id == id);
            return entry.key ?? "";
        }

        #region Table
        private void AddTableField(bool speaker)
        {
            List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;
            int index = GetTableIndex(optionData[0].ToString(), optionData[1].GetGuid());
            string name = speaker ? "Speaker Table" : "Text Table";
            PopupField<System.Guid> field = new(name, tableIds, index + 1) { name = name, formatSelectedValueCallback = FormatTable, formatListItemCallback = FormatTable };
            field.AddToClassList("dlg-field");
            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnTableKeyDownEvent);
            extensionContainer.Add(field);

            if (index != -1)
            {
                LocalizationTableCollection collection = collections[index];
                List<EntryData> entries = speaker ? speakerEntries : textEntries;
                window.GetEntriesInto(collection, entries);

                if (speaker)
                {
                    PopupField<long> updatedField = extensionContainer.Q<PopupField<long>>("Speaker Entry");
                    if (updatedField != null)
                        updatedField.choices = GetEntryIds(entries);
                }
                else
                {
                    for (int i = 0; i < outputContainer.childCount; i++)
                    {
                        PopupField<long> updatedField = outputContainer[i].Q<PopupField<long>>("Text Entry");
                        if (updatedField != null)
                            updatedField.choices = GetEntryIds(entries);
                    }
                }
            }
        }

        private string FormatTable(System.Guid id)
        {
            int index = tableIds.IndexOf(id) - 1;
            return index >= 0 ? tables[index] : "";
        }

        private int GetTableIndex(string value, System.Guid guid)
        {
            LocalizationTableCollection match = collections.ResolveCollection(value, guid);
            for (int i = 0; i < collections.Count; i++)
                if (collections[i] == match) return i;
            return -1;
        }

        private void OnTableKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<System.Guid> field = evt.target.FindParentInCurrent<PopupField<System.Guid>>();
                if (field == null) return;
                DialogueSO so = window.SO;
                bool speaker = field.name == "Speaker Table";
                List<GenericData> tableOptionData = optionDatas[speaker ? 0 : 2].data;
                Undo.RecordObject(so, "Delete Table Option");

                if (speaker)
                {
                    PopupField<long> entryField = extensionContainer.Q<PopupField<long>>("Speaker Entry");

                    if (entryField != null)
                    {
                        List<GenericData> entryOptionData = optionDatas[speaker ? 1 : 3].data;
                        entryField.SetValueWithoutNotify(0);
                        entryOptionData[0] = new(GenericData.DataType.String);
                        entryOptionData[1] = new(GenericData.DataType.Long);
                        TextField textField = textsElement.Q<TextField>("Speaker Text");

                        if (textField != null)
                    {
                        textField.SetValueWithoutNotify("");
                        RefreshPreview(textField);
                    }

                        speakerEntries.Clear();
                    }
                }
                else
                {
                    for (int i = 0; i < portDatas.Count; i++)
                    {
                        List<GenericData> entryOptionData = portDatas[i].data;
                        entryOptionData[0] = new(GenericData.DataType.String);
                        entryOptionData[1] = new(GenericData.DataType.Long);
                        PopupField<long> entryField = outputContainer[i].Q<PopupField<long>>("Text Entry");
                        if (entryField != null)
                            entryField.SetValueWithoutNotify(0);
                        if (textsElement[i + 1] is TextField textField)
                            textField.SetValueWithoutNotify("");
                    }

                    textEntries.Clear();
                }

                field.value = System.Guid.Empty;
                tableOptionData[0] = new(GenericData.DataType.String);
                tableOptionData[1] = new(GenericData.DataType.Guid);
                RefreshPreviewEditability();
                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<System.Guid> evt)
        {
            int index = tableIds.IndexOf(evt.newValue) - 1;

            if (index != -1)
            {
                DialogueSO so = window.SO;
                VisualElement target = evt.target as VisualElement;
                bool speaker = target.name == "Speaker Table";
                LocalizationTableCollection collection = collections[index];
                Undo.RecordObject(so, "Change Table");
                List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

                if (optionData[1].GetGuid() != collection.TableCollectionNameReference.TableCollectionNameGuid)
                {
                    if (speaker)
                    {
                        PopupField<long> entryField = extensionContainer.Q<PopupField<long>>("Speaker Entry");

                        if (entryField != null)
                        {
                            List<GenericData> entryOptionData = optionDatas[speaker ? 1 : 3].data;
                            entryField.SetValueWithoutNotify(0);
                            entryOptionData[0] = new(GenericData.DataType.String);
                            entryOptionData[1] = new(GenericData.DataType.Long);
                            TextField textField = textsElement.Q<TextField>("Speaker Text");

                            if (textField != null)
                    {
                        textField.SetValueWithoutNotify("");
                        RefreshPreview(textField);
                    }

                            speakerEntries.Clear();
                        }
                    }
                    else
                    {
                        for (int i = 0; i < portDatas.Count; i++)
                        {
                            List<GenericData> entryOptionData = portDatas[i].data;
                            entryOptionData[0] = new(GenericData.DataType.String);
                            entryOptionData[1] = new(GenericData.DataType.Long);
                            PopupField<long> entryField = outputContainer[i].Q<PopupField<long>>("Text Entry");
                            if (entryField != null)
                                entryField.SetValueWithoutNotify(0);
                            if (textsElement[i + 1] is TextField textField)
                                textField.SetValueWithoutNotify("");
                        }

                        textEntries.Clear();
                    }
                }

                List<EntryData> entries = speaker ? speakerEntries : textEntries;
                window.GetEntriesInto(collection, entries);

                if (speaker)
                {
                    PopupField<long> updatedField = extensionContainer.Q<PopupField<long>>("Speaker Entry");
                    if (updatedField != null)
                        updatedField.choices = GetEntryIds(entries);
                }
                else
                {
                    for (int i = 0; i < outputContainer.childCount; i++)
                    {
                        PopupField<long> updatedField = outputContainer[i].Q<PopupField<long>>("Text Entry");
                        if (updatedField != null)
                            updatedField.choices = GetEntryIds(entries);
                    }
                }

                optionData[0] = new(collection.TableCollectionName);
                optionData[1] = new(collection.TableCollectionNameReference.TableCollectionNameGuid);
                RefreshPreviewEditability();
                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }
        #endregion

        #region Entry
        private void AddSpeakerEntryField()
        {
            List<GenericData> optionData = optionDatas[1].data;
            int index = FindEntryIndex(speakerEntries, optionData);
            string name = "Speaker Entry";
            PopupField<long> field = new(name, GetEntryIds(speakerEntries), index + 1)
            {
                name = name,
                formatSelectedValueCallback = id => FormatEntry(true, id),
                formatListItemCallback = id => FormatEntry(true, id)
            };
            field.AddToClassList("dlg-field");
            field.RegisterValueChangedCallback(ChangedSpeakerCallback);
            field.RegisterCallback<KeyDownEvent>(OnEntryKeyDownEvent);
            extensionContainer.Add(field);

            if (index != -1)
            {
                EntryData entryData = speakerEntries[index];
                TextField textField = textsElement.Q<TextField>("Speaker Text");

                if (textField != null)
                {
                    textField.SetValueWithoutNotify(entryData.GetText(window.Language));
                    RefreshPreview(textField);
                }
            }
        }

        private void OnEntryKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<long> field = evt.target.FindParentInCurrent<PopupField<long>>();
                if (field == null) return;
                DialogueSO so = window.SO;
                List<GenericData> optionData;
                Undo.RecordObject(so, "Delete Entry Option");

                if (field.name == "Speaker Entry")
                {
                    optionData = optionDatas[1].data;
                    TextField textField = textsElement.Q<TextField>("Speaker Text");

                    if (textField != null)
                    {
                        textField.SetValueWithoutNotify("");
                        RefreshPreview(textField);
                    }
                }
                else
                {
                    Port port = evt.target.FindParent<Port>();
                    int index = port.parent.IndexOf(port);
                    optionData = portDatas[index].data;
                    if (textsElement[index + 1] is TextField textField)
                        textField.SetValueWithoutNotify("");
                }

                field.SetValueWithoutNotify(0);
                optionData[0] = new(GenericData.DataType.String);
                optionData[1] = new(GenericData.DataType.Long);
                RefreshPreviewEditability();
                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }

        private void ChangedSpeakerCallback(ChangeEvent<long> evt)
        {
            int index = speakerEntries.FindIndex(entry => entry.id == evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                List<GenericData> optionData = optionDatas[1].data;
                Undo.RecordObject(so, "Change Speaker Entry");
                EntryData entryData = speakerEntries[index];
                optionData[0] = new(entryData.key);
                optionData[1] = new(entryData.id);
                TextField textField = textsElement.Q<TextField>("Speaker Text");

                if (textField != null)
                {
                    textField.SetValueWithoutNotify(entryData.GetText(window.Language));
                    RefreshPreview(textField);
                }

                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }
        #endregion

        #region Text Entry
        private void CreateTextEntry()
        {
            DialogueSO so = window.SO;
            Undo.RecordObject(so, "Create Choice Entry");
            DataWrapper portOption = new(
                new(GenericData.DataType.String),
                new(GenericData.DataType.Long),
                new(GenericData.DataType.Bool)
            );
            AddTextEntryField(portOption.data);
            portDatas.Add(portOption);
            RefreshExpandedState();
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private VisualElement AddTextEntryField(List<GenericData> optionData)
        {
            int index = FindEntryIndex(textEntries, optionData);
            Port port = CreateOutputPort(false);
            TextField textField = AddTextField("Text", "Text Text");
            VisualElement portElement = new();
            VisualElement groupContainer = new();
            VisualElement entryContainer = new();
            VisualElement itemContainer = new();
            VisualElement buttonContainer = new();
            VisualElement line = new();
            portElement.AddToClassList("dlg-port-row");
            groupContainer.AddToClassList("dlg-port-col");
            entryContainer.AddToClassList("dlg-port-entry");
            itemContainer.AddToClassList("dlg-port-items");
            buttonContainer.AddToClassList("dlg-port-buttons");
            line.AddToClassList("dlg-port-line");
            string name = "Text Entry";
            PopupField<long> field = new(name, GetEntryIds(textEntries), index + 1)
            {
                name = name,
                formatSelectedValueCallback = id => FormatEntry(false, id),
                formatListItemCallback = id => FormatEntry(false, id)
            };
            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedTextCallback);
            field.RegisterCallback<KeyDownEvent>(OnEntryKeyDownEvent);
            Toggle hide = new()
            {
                name = "Hide",
                value = optionData[2].GetBool(),
                tooltip = "Only passes the option if conditions are met."
            };
            hide.RegisterValueChangedCallback(ChangedTextCallback);
            Button createVariableButton = new(() =>
                DialogueVariableSchemaUtility.ShowMenu((fieldId, type) =>
                    CreateConditionField(itemContainer, fieldId, type))) { text = "+" };
            Button removeButton = new(() =>
            {
                if (RemovePort(port))
                    textsElement.Remove(textField);
            })
            { text = "X" };
            entryContainer.Add(field);
            entryContainer.Add(hide);
            buttonContainer.Add(createVariableButton);
            buttonContainer.Add(removeButton);
            groupContainer.Add(entryContainer);
            groupContainer.Add(itemContainer);
            groupContainer.Add(buttonContainer);
            portElement.Add(RowDrag.CreateHandle(port, 0, SwapPort, () => RefreshPorts()));
            portElement.Add(groupContainer);
            portElement.Add(line);
            RegisterPortJump(line, port);
            port.style.height = StyleKeyword.Auto;
            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(portElement);

            if (index != -1)
            {
                EntryData entryData = textEntries[index];
                textField.SetValueWithoutNotify(entryData.GetText(window.Language));
            }

            RefreshPreview(textField);
            return itemContainer;
        }

        private void CreateConditionField(VisualElement itemContainer, int fieldId, System.Type type)
        {
            if (type == typeof(float))
                CreateConditionFloatField(itemContainer, fieldId);
            else if (type == typeof(bool))
                CreateConditionBoolField(itemContainer, fieldId);
        }

        private void RemoveConditionField(VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            VisualElement itemContainer = itemElement.parent;
            Port port = itemContainer.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = itemContainer.IndexOf(itemElement);

            if (itemIndex != -1)
            {
                Undo.RecordObject(so, "Remove Condition Field");
                itemContainer.Remove(itemElement);
                List<GenericData> portData = portDatas[portIndex].data;
                portData.RemoveRange(3 + itemIndex * 3, 3);

                if (itemContainer.childCount == 0)
                {
                    Toggle hide = port.Q<Toggle>("Hide");
                    hide.style.display = DisplayStyle.None;
                }

                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }

        private void OnConditionKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<string> field = evt.target.FindParentInCurrent<PopupField<string>>();
                if (field == null) return;
                DialogueSO so = window.SO;
                VisualElement itemElement = field.FindParent<VisualElement>("Item Element");
                Port port = itemElement.FindParent<Port>();
                Undo.RecordObject(so, "Delete Condition Option");
                field.value = "";
                int portIndex = port.parent.IndexOf(port);
                int itemIndex = 3 + itemElement.parent.IndexOf(itemElement) * 3;
                portDatas[portIndex].data[itemIndex] = new(GenericData.DataType.Int);
                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }

        private void ChangedTextCallback(ChangeEvent<bool> evt)
        {
            DialogueSO so = window.SO;
            Port port = evt.target.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            Undo.RecordObject(so, "Change Text Entry");
            portDatas[portIndex].data[2] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private void ChangedTextCallback(ChangeEvent<long> evt)
        {
            int index = textEntries.FindIndex(entry => entry.id == evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                Port port = evt.target.FindParent<Port>();
                int portIndex = port.parent.IndexOf(port);
                Undo.RecordObject(so, "Change Text Entry");
                List<GenericData> portData = portDatas[portIndex].data;
                EntryData entryData = textEntries[index];
                portData[0] = new(entryData.key);
                portData[1] = new(entryData.id);
                TextField textField = textsElement.Query<TextField>("Text Text").AtIndex(portIndex);

                if (textField != null)
                {
                    textField.SetValueWithoutNotify(entryData.GetText(window.Language));
                    RefreshPreview(textField);
                }

                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }

        private void ChangedConditionCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;
            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");
            Port port = itemElement.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = 3 + itemElement.parent.IndexOf(itemElement) * 3;
            Undo.RecordObject(so, "Change Condition Option");
            List<GenericData> data = portDatas[portIndex].data;
            int fieldId = DialogueVariableSchemaUtility.GetFieldId(evt.newValue);
            System.Type selectedType = DialogueVariableSchemaUtility.GetValueType(fieldId);
            GenericData.DataType currentType = data[itemIndex + 1].Type;

            if (selectedType == typeof(float) && currentType != GenericData.DataType.Float)
            {
                data[itemIndex] = new GenericData(fieldId);
                data[itemIndex + 1] = new GenericData(0f);
                data[itemIndex + 2] = new GenericData(ValueCheckType.Less);
                ReplaceConditionRow(itemElement, (itemIndex - 3) / 3,
                    GetConditionFloatField(fieldId, 0f, ValueCheckType.Less));
            }
            else if (selectedType == typeof(bool) && currentType != GenericData.DataType.Bool)
            {
                data[itemIndex] = new GenericData(fieldId);
                data[itemIndex + 1] = new GenericData(false);
                data[itemIndex + 2] = new GenericData(GenericData.DataType.Enum);
                ReplaceConditionRow(itemElement, (itemIndex - 3) / 3,
                    GetConditionBoolField(fieldId, false));
            }
            else
            {
                data[itemIndex] = new(fieldId);
            }

            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private static void ReplaceConditionRow(VisualElement oldRow, int index, VisualElement newRow)
        {
            VisualElement parent = oldRow.parent;
            parent.Remove(oldRow);
            parent.Insert(index, newRow);
        }
        #endregion

        #region Float
        private void CreateConditionFloatField(VisualElement itemContainer, int fieldId)
        {
            Port port = itemContainer.FindParent<Port>();
            DialogueSO so = window.SO;
            int portIndex = port.parent.IndexOf(port);
            List<GenericData> portData = portDatas[portIndex].data;
            Undo.RecordObject(so, "Add Condition Float Field");
            itemContainer.Add(GetConditionFloatField(fieldId, 0, ValueCheckType.Less));
            portData.Add(new(fieldId));
            portData.Add(new(GenericData.DataType.Float));
            portData.Add(new(GenericData.DataType.Enum));
            Toggle hide = port.Q<Toggle>("Hide");
            hide.style.display = DisplayStyle.Flex;
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private VisualElement GetConditionFloatField(int fieldId, float value, ValueCheckType type)
        {
            VisualElement itemElement = new() { name = "Item Element" };
            itemElement.AddToClassList("dlg-row");
            DialogueVariableSchemaUtility.GetKeys(conditions);
            int index = conditions.IndexOf(DialogueVariableSchemaUtility.GetLabel(fieldId));
            PopupField<string> field = new("Variable", conditions, index);
            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedConditionCallback);
            field.RegisterCallback<KeyDownEvent>(OnConditionKeyDownEvent);
            FloatField floatField = new() { value = value };
            floatField.AddToClassList("dlg-num");
            floatField.RegisterValueChangedCallback(ChangedCallback);
            EnumField typeField = new(type);
            typeField.AddToClassList("dlg-enum");
            typeField.RegisterValueChangedCallback(ChangedCallback);
            Button remove = new(() => RemoveConditionField(itemElement)) { text = "-" };
            itemElement.Add(field);
            itemElement.Add(floatField);
            itemElement.Add(typeField);
            itemElement.Add(remove);
            return itemElement;
        }

        private void ChangedCallback(ChangeEvent<float> evt)
        {
            DialogueSO so = window.SO;
            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");
            Port port = itemElement.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = 3 + itemElement.parent.IndexOf(itemElement) * 3 + 1;
            Undo.RecordObject(so, "Change Condition Float Option");
            portDatas[portIndex].data[itemIndex] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<System.Enum> evt)
        {
            DialogueSO so = window.SO;
            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");
            Port port = itemElement.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = 3 + itemElement.parent.IndexOf(itemElement) * 3 + 2;
            Undo.RecordObject(so, "Change Condition Type Option");
            portDatas[portIndex].data[itemIndex] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }
        #endregion

        #region Bool
        private void CreateConditionBoolField(VisualElement itemContainer, int fieldId)
        {
            Port port = itemContainer.FindParent<Port>();
            DialogueSO so = window.SO;
            int portIndex = port.parent.IndexOf(port);
            List<GenericData> portData = portDatas[portIndex].data;
            Undo.RecordObject(so, "Add Condition Bool Field");
            itemContainer.Add(GetConditionBoolField(fieldId, false));
            portData.Add(new(fieldId));
            portData.Add(new(GenericData.DataType.Bool));
            portData.Add(new(GenericData.DataType.Enum));
            Toggle hide = port.Q<Toggle>("Hide");
            hide.style.display = DisplayStyle.Flex;
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private VisualElement GetConditionBoolField(int fieldId, bool value)
        {
            VisualElement itemElement = new() { name = "Item Element" };
            itemElement.AddToClassList("dlg-row");
            DialogueVariableSchemaUtility.GetKeys(conditions);
            int index = conditions.IndexOf(DialogueVariableSchemaUtility.GetLabel(fieldId));
            PopupField<string> field = new("Variable", conditions, index);
            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedConditionCallback);
            field.RegisterCallback<KeyDownEvent>(OnConditionKeyDownEvent);
            Toggle toggle = new() { value = value };
            toggle.RegisterValueChangedCallback(ChangedCallback);
            Button remove = new(() => RemoveConditionField(itemElement)) { text = "-" };
            itemElement.Add(field);
            itemElement.Add(toggle);
            itemElement.Add(remove);
            return itemElement;
        }

        private void ChangedCallback(ChangeEvent<bool> evt)
        {
            DialogueSO so = window.SO;
            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");
            Port port = itemElement.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = 3 + itemElement.parent.IndexOf(itemElement) * 3 + 1;
            Undo.RecordObject(so, "Change Condition Bool Option");
            portDatas[portIndex].data[itemIndex] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }
        #endregion

        #region Message
        private void MessageChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;
            Undo.RecordObject(so, "Change Choice Message");
            optionDatas[3].data[0] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private void AddMessageField()
        {
            DialogueSO so = window.SO;
            List<GenericData> optionData = optionDatas[3].data;
            TextField field = new("Message") { value = optionData[0].ToString() };
            field.AddToClassList("dlg-field");
            field.RegisterValueChangedCallback(MessageChangedCallback);
            extensionContainer.Add(field);
        }
        #endregion
    }
}
