using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class DialogueNode : BaseNode
    {
        // optionDatas: [0] speaker table(name, GUID), [1] speaker entry(key, ID), [2] text table(name, GUID), [3] text entry(key, ID), [4] message.
        private readonly List<string> tables;
        private readonly List<System.Guid> tableIds = new();
        private readonly List<EntryData> speakerEntries = new();
        private readonly List<EntryData> textEntries = new();

        private readonly IReadOnlyList<LocalizationTableCollection> collections;

        private readonly VisualElement textsElement = new();

        public DialogueNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            tables = window.Tables;
            collections = window.collections;
            collections.SetTableIds(tableIds);
        }

        public override void SetPorts()
        {
            SetDefault();
            CreateInputPort();
            CreateOutputPort();
            contentContainer[0].Insert(2, textsElement);
            AddTextField("Speaker", "Speaker Text");
            AddTextField("Text", "Text Text");
            SetPreview();
        }

        protected override void BuildExtension() => SetOptions();

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

                    if (guid == System.Guid.Empty) guid = collections.ResolveCollection(reference.TableCollectionName, guid)?.TableCollectionNameReference.TableCollectionNameGuid ?? guid;

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

                    if (guid == System.Guid.Empty) guid = collections.ResolveCollection(reference.TableCollectionName, guid)?.TableCollectionNameReference.TableCollectionNameGuid ?? guid;

                    textTable = new(new(reference.TableCollectionName), new(guid));
                }

                DataWrapper speakerEntry = new(new(GenericData.DataType.String), new(GenericData.DataType.Long));
                DataWrapper textEntry = new(new(GenericData.DataType.String), new(GenericData.DataType.Long));
                DataWrapper message = new(new GenericData(GenericData.DataType.String));

                optionDatas.Add(speakerTable);
                optionDatas.Add(speakerEntry);
                optionDatas.Add(textTable);
                optionDatas.Add(textEntry);
                optionDatas.Add(message);
            }

            if (portDatas.Count == 0) portDatas.Add(new());
        }

        private void SetOptions()
        {
            AddTableField(true);
            AddEntryField(true);
            AddTableField(false);
            AddEntryField(false);
            AddMessageField();
        }

        private void AddTextField(string title, string name)
        {
            TextField field = new(title)
            {
                name = name,
                isReadOnly = true,
                multiline = name == "Text Text",
            };
            field.isDelayed = true;
            field.tooltip = "Edit the selected locale's String Table entry (changes commit on focus loss).";
            field.RegisterValueChangedCallback(evt => OnPreviewChanged(field, evt.newValue));
            field.AddToClassList("dlg-preview");
            textsElement.Add(field);
        }

        private void SetPreview()
        {
            SetPreviewField("Speaker Text", optionDatas[0].data, optionDatas[1].data);
            SetPreviewField("Text Text", optionDatas[2].data, optionDatas[3].data);
        }

        private void SetPreviewField(string fieldName, List<GenericData> table, List<GenericData> entry)
        {
            TextField field = textsElement.Q<TextField>(fieldName);

            if (field == null) return;

            field.SetValueWithoutNotify(ResolvePreview(table[0].ToString(), table[1].GetGuid(), entry[0].ToString(), entry[1].GetLong()));
            field.isReadOnly = !window.CanEditLocalizedText(table, entry);
        }

        private void OnPreviewChanged(TextField field, string value)
        {
            bool speaker = field.name == "Speaker Text";
            List<GenericData> table = optionDatas[speaker ? 0 : 2].data;
            List<GenericData> entry = optionDatas[speaker ? 1 : 3].data;

            if (!window.EditLocalizedText(table, entry, value)) SetPreviewField(field.name, table, entry);
        }

        private string ResolvePreview(string tableName, System.Guid tableGuid, string key, long id)
        {
            LocalizationTableCollection collection = collections.ResolveCollection(tableName, tableGuid, id, key);

            return collection == null ? "" : new EntryData(id, key, collection.Tables).GetText(window.Language);
        }

        private static List<long> GetEntryIds(List<EntryData> entries)
        {
            List<long> ids = new() { 0 };

            foreach (EntryData entry in entries) ids.Add(entry.id);

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

        private void AddTableField(bool speaker)
        {
            List<EntryData> entries = speaker ? speakerEntries : textEntries;
            List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;
            int index = GetTableIndex(optionData[0].ToString(), optionData[1].GetGuid());
            string name = speaker ? "Speaker Table" : "Text Table";
            PopupField<System.Guid> field = new(name, tableIds, index + 1) { name = name, formatSelectedValueCallback = FormatTable, formatListItemCallback = FormatTable };

            field.AddToClassList("dlg-field");
            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnTableKeyDownEvent);
            extensionContainer.Add(field);

            if (index != -1) window.GetEntriesInto(collections[index], entries);
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
                PopupField<System.Guid> tableField = evt.target.FindParentInCurrent<PopupField<System.Guid>>();

                if (tableField == null) return;

                DialogueSO so = window.SO;
                bool speaker = tableField.name == "Speaker Table";
                PopupField<long> entryField = extensionContainer.Q<PopupField<long>>(speaker ? "Speaker Entry" : "Text Entry");

                if (entryField == null) return;

                List<GenericData> tableOptionData = optionDatas[speaker ? 0 : 2].data;
                List<GenericData> entryOptionData = optionDatas[speaker ? 1 : 3].data;

                Undo.RecordObject(so, "Delete Table Option");
                tableField.value = System.Guid.Empty;
                entryField.SetValueWithoutNotify(0);
                tableOptionData[0] = new(GenericData.DataType.String);
                tableOptionData[1] = new(GenericData.DataType.Guid);
                entryOptionData[0] = new(GenericData.DataType.String);
                entryOptionData[1] = new(GenericData.DataType.Long);

                if (speaker)
                {
                    TextField textField = textsElement.Q<TextField>("Speaker Text");

                    if (textField != null) textField.SetValueWithoutNotify("");

                    speakerEntries.Clear();
                }
                else
                {
                    TextField textField = textsElement.Q<TextField>("Text Text");

                    if (textField != null) textField.SetValueWithoutNotify("");

                    textEntries.Clear();
                }

                EditorUtility.SetDirty(so);
                window.SetUnsaved();
                SetPreview();
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
                    PopupField<long> entryField = extensionContainer.Q<PopupField<long>>(speaker ? "Speaker Entry" : "Text Entry");

                    if (entryField != null)
                    {
                        List<GenericData> entryOptionData = optionDatas[speaker ? 1 : 3].data;
                        entryField.SetValueWithoutNotify(0);
                        entryOptionData[0] = new(GenericData.DataType.String);
                        entryOptionData[1] = new(GenericData.DataType.Long);

                        if (speaker)
                        {
                            TextField textField = textsElement.Q<TextField>("Speaker Text");

                            if (textField != null) textField.SetValueWithoutNotify("");

                            speakerEntries.Clear();
                        }
                        else
                        {
                            TextField textField = textsElement.Q<TextField>("Text Text");

                            if (textField != null) textField.SetValueWithoutNotify("");

                            textEntries.Clear();
                        }
                    }

                    List<EntryData> entries = speaker ? speakerEntries : textEntries;
                    window.GetEntriesInto(collection, entries);
                    PopupField<long> updatedField = extensionContainer.Q<PopupField<long>>(speaker ? "Speaker Entry" : "Text Entry");

                    if (updatedField != null) updatedField.choices = GetEntryIds(entries);
                }

                optionData[0] = new(collection.TableCollectionName);
                optionData[1] = new(collection.TableCollectionNameReference.TableCollectionNameGuid);
                EditorUtility.SetDirty(so);
                window.SetUnsaved();
                SetPreview();
            }
        }

        private void AddEntryField(bool speaker)
        {
            List<EntryData> entries = speaker ? speakerEntries : textEntries;
            List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;
            int index = FindEntryIndex(entries, optionData);
            string name = speaker ? "Speaker Entry" : "Text Entry";
            PopupField<long> field = new(name, GetEntryIds(entries), index + 1)
            {
                name = name,
                formatSelectedValueCallback = id => FormatEntry(speaker, id),
                formatListItemCallback = id => FormatEntry(speaker, id)
            };
            field.AddToClassList("dlg-field");
            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnEntryKeyDownEvent);
            extensionContainer.Add(field);

            if (index != -1)
            {
                EntryData entryData = entries[index];

                if (speaker)
                {
                    TextField textField = textsElement.Q<TextField>("Speaker Text");

                    if (textField != null) textField.SetValueWithoutNotify(entryData.GetText(window.Language));
                }
                else
                {
                    TextField textField = textsElement.Q<TextField>("Text Text");

                    if (textField != null) textField.SetValueWithoutNotify(entryData.GetText(window.Language));
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
                bool speaker = field.name == "Speaker Entry";
                List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;

                Undo.RecordObject(so, "Delete Entry Option");
                field.SetValueWithoutNotify(0);
                optionData[0] = new(GenericData.DataType.String);
                optionData[1] = new(GenericData.DataType.Long);

                if (speaker)
                {
                    TextField textField = textsElement.Q<TextField>("Speaker Text");

                    if (textField != null) textField.SetValueWithoutNotify("");
                }
                else
                {
                    TextField textField = textsElement.Q<TextField>("Text Text");

                    if (textField != null) textField.SetValueWithoutNotify("");
                }

                EditorUtility.SetDirty(so);
                window.SetUnsaved();
                SetPreview();
            }
        }

        private void ChangedCallback(ChangeEvent<long> evt)
        {
            if (evt.target is PopupField<long> target)
            {
                bool speaker = target.name == "Speaker Entry";
                List<EntryData> entries = speaker ? speakerEntries : textEntries;
                List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;
                int index = entries.FindIndex(entry => entry.id == evt.newValue);

                if (index != -1)
                {
                    DialogueSO so = window.SO;
                    Undo.RecordObject(so, "Change Entry");
                    EntryData entryData = entries[index];
                    optionData[0] = new(entryData.key);
                    optionData[1] = new(entryData.id);

                    if (speaker)
                    {
                        TextField textField = textsElement.Q<TextField>("Speaker Text");

                        if (textField != null) textField.SetValueWithoutNotify(entryData.GetText(window.Language));
                    }
                    else
                    {
                        TextField textField = textsElement.Q<TextField>("Text Text");

                        if (textField != null) textField.SetValueWithoutNotify(entryData.GetText(window.Language));
                    }

                    EditorUtility.SetDirty(so);
                    window.SetUnsaved();
                    SetPreview();
                }
            }
        }

        private void MessageChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;
            Undo.RecordObject(so, "Change Choice Message");
            optionDatas[4].data[0] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private void AddMessageField()
        {
            TextField field = new("Message") { value = optionDatas[4].data[0].ToString() };
            field.AddToClassList("dlg-field");
            field.RegisterValueChangedCallback(MessageChangedCallback);
            extensionContainer.Add(field);
        }
    }
}
