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
        private readonly List<string> tables = new();
        private readonly List<EntryData> speakerEntries = new();
        private readonly List<EntryData> textEntries = new();

        private IReadOnlyList<LocalizationTableCollection> collections;

        public DialogueNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            SetTables();
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
            NodeData data = window.GetNode(GUID);

            if (data.portDatas.Count == 0)
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

                data.optionDatas.Add(speakerTable);
                data.optionDatas.Add(speakerEntry);

                data.optionDatas.Add(textTable);
                data.optionDatas.Add(textEntry);

                data.optionDatas.Add(message);

                data.portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            AddTableField(speakerEntries, true);
            AddEntryField(speakerEntries, true);

            AddTableField(textEntries, false);
            AddEntryField(textEntries, false);

            AddMessageField();
        }

        #region Table
        private void ChangedCallback(ChangeEvent<string> evt, List<EntryData> entries, bool speaker)
        {
            int index = tables.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                NodeData data = window.GetNode(GUID);

                SetEntries(collections[index], entries);

                Undo.RecordObject(so, speaker ? "Change Speaker Table" : "Change Text Table");

                List<GenericData> optionData = data.optionDatas[speaker ? 0 : 2].data;

                optionData[0] = new(collections[index].TableCollectionName);
                optionData[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void AddTableField(List<EntryData> entries, bool speaker)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            List<GenericData> optionData = data.optionDatas[speaker ? 0 : 2].data;

            int index = GetTableIndex(optionData[0].ToString(), optionData[1].TryGetGuid(out System.Guid guid) ? guid : default);

            PopupField<string> field = new(speaker ? "Speaker Table" : "Entry Table", tables, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, entries, speaker));

            extensionContainer.Add(field);

            if (index != -1)
            {
                SetEntries(collections[index], entries);

                optionData[0] = new(collections[index].TableCollectionName);
                optionData[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);
            }
        }

        private void SetTables()
        {
            collections = LocalizationEditorSettings.GetStringTableCollections();

            tables.Clear();

            if (collections != null)
            {
                foreach (LocalizationTableCollection collection in collections)
                {
                    string tableName = collection.TableCollectionName;
                    string group = collection.Group;

                    tables.Add(string.IsNullOrEmpty(group) ? tableName : $"{group}/{tableName}");
                }
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
        #endregion

        #region Entry
        private void ChangedCallback(ChangeEvent<EntryData> evt, List<EntryData> entries, bool speaker)
        {
            int index = entries.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                NodeData data = window.GetNode(GUID);

                List<GenericData> optionData = data.optionDatas[speaker ? 1 : 3].data;

                Undo.RecordObject(so, $"Change Speaker Entry");

                optionData[0] = new(entries[index].key);
                optionData[1] = new(entries[index].id);

                if (index != -1 && evt.target is PopupField<EntryData> dropdown) dropdown.tooltip = entries[index].tooltip;

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void AddEntryField(List<EntryData> entries, bool speaker)
        {
            NodeData data = window.GetNode(GUID);

            List<GenericData> datas = data.optionDatas[speaker ? 1 : 3].data;

            int index = entries.IndexOf(new EntryData(datas[1].TryGetLong(out long result) ? result : 0, datas[0].ToString()));

            PopupField<EntryData> field = new(speaker ? "Speaker Entry" : "Text Entry", entries, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, entries, speaker));

            extensionContainer.Add(field);

            if (index != -1)
            {
                field.tooltip = entries[index].tooltip;

                datas[0] = new(entries[index].key);
                datas[1] = new(entries[index].id);
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
        #endregion

        #region Message
        private void MessageChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            Undo.RecordObject(so, "Change Choice Message");

            data.optionDatas[4].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddMessageField()
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            TextField field = new("Message") { value = data.optionDatas[4].data[0].ToString() };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(MessageChangedCallback);

            extensionContainer.Add(field);
        }
        #endregion
    }
}