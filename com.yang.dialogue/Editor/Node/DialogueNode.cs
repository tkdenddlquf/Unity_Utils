using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (Unused)
    /// 
    /// Option Data (0 : Speaker Table)
    /// 0 : Name - string
    /// 1 : Guid - guid
    /// 
    /// Option Data (1 : Speaker Entry)
    /// 0 : Key - string
    /// 1 : ID - long
    /// 
    /// Option Data (2 : Text Table)
    /// 0 : Name - string
    /// 1 : Guid - guis
    /// 
    /// Option Data (3 : Text Entry)
    /// 0 : Key - string
    /// 1 : ID - long
    /// 
    /// Option Data (4 : Message)
    /// 0 : Text - string
    /// </summary>
    public class DialogueNode : BaseNode
    {
        private readonly List<string> tables;
        private readonly List<EntryData> speakerEntries = new();
        private readonly List<EntryData> textEntries = new();

        private readonly IReadOnlyList<LocalizationTableCollection> collections;

        public DialogueNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            tables = window.Tables;
            collections = window.collections;
        }

        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();

            SetOptions();
        }

        private void SetDefault()
        {
            if (portDatas.Count == 0)
            {
                DataWrapper speakerTable = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Guid)
                );

                DataWrapper speakerEntry = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Long)
                );

                DataWrapper textTable = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Guid)
                );

                DataWrapper textEntry = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Long)
                );

                DataWrapper message = new(new GenericData(GenericData.DataType.String));

                optionDatas.Add(speakerTable);
                optionDatas.Add(speakerEntry);

                optionDatas.Add(textTable);
                optionDatas.Add(textEntry);

                optionDatas.Add(message);

                portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            AddTableField(true);
            AddEntryField(true);

            AddTableField(false);
            AddEntryField(false);

            AddMessageField();
        }

        #region Table
        private void AddTableField(bool speaker)
        {
            List<EntryData> entries = speaker ? speakerEntries : textEntries;
            List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

            int index = GetTableIndex(optionData[0].ToString(), optionData[1].TryGetGuid(out System.Guid guid) ? guid : default);

            string name = speaker ? "Speaker Table" : "Text Table";
            PopupField<string> field = new(name, tables, index) { name = name };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnTableKeyDownEvent);

            extensionContainer.Add(field);

            if (index != -1)
            {
                SetEntries(collections[index], entries);

                optionData[0] = new(collections[index].TableCollectionName);
                optionData[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);
            }
        }

        private int GetTableIndex(string value, System.Guid guid)
        {
            for (int i = 0; i < collections.Count; i++)
            {
                LocalizationTableCollection collection = collections[i];

                if (collection.TableCollectionName == value || collection.TableCollectionNameReference.TableCollectionNameGuid == guid) return i;
            }

            return -1;
        }

        private void OnTableKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<string> field = FindParentInCurrent<PopupField<string>>(evt.target as VisualElement);

                if (field == null) return;

                DialogueSO so = window.SO;

                List<GenericData> optionData = optionDatas[field.name == "Speaker Table" ? 0 : 2].data;

                Undo.RecordObject(so, "Delete Table Option");

                field.value = "";

                optionData[0] = new(GenericData.DataType.String);
                optionData[1] = new(GenericData.DataType.Guid);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<string> evt)
        {
            int index = tables.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;

                VisualElement target = evt.target as VisualElement;
                bool speaker = target.name == "Speaker Table";

                SetEntries(collections[index], speaker ? speakerEntries : textEntries);

                Undo.RecordObject(so, speaker ? "Change Speaker Table" : "Change Text Table");

                List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

                optionData[0] = new(collections[index].TableCollectionName);
                optionData[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
        #endregion

        #region Entry
        private void AddEntryField(bool speaker)
        {
            List<EntryData> entries = speaker ? speakerEntries : textEntries;
            List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;

            int index = entries.IndexOf(new EntryData(optionData[1].TryGetLong(out long result) ? result : 0, optionData[0].ToString()));

            string name = speaker ? "Speaker Entry" : "Text Entry";
            PopupField<EntryData> field = new(name, entries, index) { name = name };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnEntryKeyDownEvent);

            extensionContainer.Add(field);

            if (index != -1)
            {
                field.tooltip = entries[index].tooltip;

                optionData[0] = new(entries[index].key);
                optionData[1] = new(entries[index].id);
            }
        }

        private void SetEntries(LocalizationTableCollection collection, List<EntryData> entries)
        {
            entries.Clear();

            if (collection != null)
            {
                foreach (SharedTableData.SharedTableEntry current in collection.SharedData.Entries)
                {
                    string tooltip = "";

                    if (collection.GetTable(Application.systemLanguage) is StringTable stringTable)
                    {
                        StringTableEntry entry = stringTable.GetEntry(current.Key);

                        if (entry != null) tooltip = entry.Value;
                    }

                    EntryData data = new(current.Id, current.Key, tooltip);

                    entries.Add(data);
                }
            }
        }

        private void OnEntryKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<EntryData> field = FindParentInCurrent<PopupField<EntryData>>(evt.target as VisualElement);

                if (field == null) return;

                DialogueSO so = window.SO;

                List<GenericData> optionData = optionDatas[field.name == "Speaker Entry" ? 1 : 3].data;

                Undo.RecordObject(so, "Delete Entry Option");

                field.value = default;

                optionData[0] = new(GenericData.DataType.String);
                optionData[1] = new(GenericData.DataType.Long);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<EntryData> evt)
        {
            if (evt.target is PopupField<EntryData> target)
            {
                bool speaker = target.name == "Speaker Entry";

                List<EntryData> entries = speaker ? speakerEntries : textEntries;
                List<GenericData> optionData = optionDatas[speaker ? 1 : 3].data;

                int index = entries.IndexOf(evt.newValue);

                if (index != -1)
                {
                    DialogueSO so = window.SO;

                    Undo.RecordObject(so, "Change Speaker Entry");

                    optionData[0] = new(entries[index].key);
                    optionData[1] = new(entries[index].id);

                    target.tooltip = entries[index].tooltip;

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }
        #endregion

        #region Message
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

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(MessageChangedCallback);

            extensionContainer.Add(field);
        }
        #endregion
    }
}