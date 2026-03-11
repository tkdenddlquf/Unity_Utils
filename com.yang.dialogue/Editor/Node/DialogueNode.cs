using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
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

            CreatePort(Direction.Input, Port.Capacity.Multi);
            CreatePort(Direction.Output, Port.Capacity.Single);

            SetOptions();
        }

        private void SetDefault()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            if (data.OptionCount == 0)
            {
                OptionData speakerTable = new(DialogueType.DIALOGUE_TYPE_000);
                OptionData speakerEntry = new(DialogueType.DIALOGUE_TYPE_001);

                OptionData textTable = new(DialogueType.DIALOGUE_TYPE_002);
                OptionData textEntry = new(DialogueType.DIALOGUE_TYPE_003);

                OptionData message = new(DialogueType.DIALOGUE_TYPE_004);

                speakerTable.datas.Add(new(GenericData.Type.String));
                speakerTable.datas.Add(new(GenericData.Type.Guid));

                speakerEntry.datas.Add(new(GenericData.Type.String));
                speakerEntry.datas.Add(new(GenericData.Type.Long));

                textTable.datas.Add(new(GenericData.Type.String));
                textTable.datas.Add(new(GenericData.Type.Guid));

                textEntry.datas.Add(new(GenericData.Type.String));
                textEntry.datas.Add(new(GenericData.Type.Long));

                message.datas.Add(new(GenericData.Type.String));

                data.AddOption(speakerTable);
                data.AddOption(speakerEntry);

                data.AddOption(textTable);
                data.AddOption(textEntry);

                data.AddOption(message);

                so.SetNode(GUID, data);
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            foreach (OptionData option in data.GetOptions())
            {
                switch (option.type)
                {
                    case DialogueType.DIALOGUE_TYPE_000:
                        AddTableField(option.datas, option.type, speakerEntries);
                        break;

                    case DialogueType.DIALOGUE_TYPE_001:
                        AddEntryField(option.datas, option.type, speakerEntries);
                        break;

                    case DialogueType.DIALOGUE_TYPE_002:
                        AddTableField(option.datas, option.type, textEntries);
                        break;

                    case DialogueType.DIALOGUE_TYPE_003:
                        AddEntryField(option.datas, option.type, textEntries);
                        break;

                    case DialogueType.DIALOGUE_TYPE_004:
                        AddMessageField(option.datas, option.type);
                        break;
                }
            }
        }

        #region Table
        private void ChangedCallback(ChangeEvent<string> evt, string type, List<EntryData> entries)
        {
            int index = tables.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                NodeData data = so.GetNode(GUID);

                int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0);

                if (optionIndex != -1)
                {
                    Undo.RecordObject(so, $"Change {type}");

                    SetEntries(collections[index], entries);

                    OptionData option = data.GetOption(optionIndex);

                    option.datas[0] = new(collections[index].TableCollectionName);
                    option.datas[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);

                    data.SetOption(optionIndex, option);
                    so.SetNode(GUID, data);

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddTableField(List<GenericData> datas, string type, List<EntryData> entries)
        {
            if (datas[1].TryGetValue(out System.Guid guid))
            {
                int index = GetTableIndex(datas[0].stringValue, guid);

                PopupField<string> field = new(type, tables, index);

                field.labelElement.style.minWidth = StyleKeyword.Auto;
                field.labelElement.style.width = StyleKeyword.Auto;

                field[1].style.minWidth = ITEM_MIN_WIDTH;

                field.RegisterValueChangedCallback(evt => ChangedCallback(evt, type, entries));

                extensionContainer.Add(field);

                if (index != -1)
                {
                    SetEntries(collections[index], entries);

                    datas[0] = new(collections[index].TableCollectionName);
                    datas[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);
                }
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
        private void ChangedCallback(ChangeEvent<EntryData> evt, string type, List<EntryData> entries)
        {
            int index = entries.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                NodeData data = so.GetNode(GUID);

                int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0);

                if (optionIndex != -1)
                {
                    Undo.RecordObject(so, $"Change {type}");

                    OptionData option = data.GetOption(optionIndex);

                    option.datas[0] = new(entries[index].key);
                    option.datas[1] = new(entries[index].id);

                    data.SetOption(optionIndex, option);
                    so.SetNode(GUID, data);

                    if (evt.target is PopupField<EntryData> dropdown) dropdown.tooltip = entries[index].tooltip;

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddEntryField(List<GenericData> datas, string type, List<EntryData> entries)
        {
            int index = entries.IndexOf(new EntryData(datas[1].longValue, datas[0].stringValue));

            PopupField<EntryData> field = new(type, entries, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, type, entries));

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
        private void ChangedCallback(ChangeEvent<string> evt, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0);

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, $"Change {type}");

                OptionData option = data.GetOption(optionIndex);

                option.datas[0] = new(evt.newValue);

                data.SetOption(optionIndex, option);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void AddMessageField(List<GenericData> datas, string type)
        {
            TextField field = new(type) { value = datas[0].stringValue };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, type));

            extensionContainer.Add(field);
        }
        #endregion
    }
}