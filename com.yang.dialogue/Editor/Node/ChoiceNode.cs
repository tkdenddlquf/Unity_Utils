using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class ChoiceNode : BaseNode
    {
        private readonly List<string> tables = new();
        private readonly List<EntryData> speakerEntries = new();
        private readonly List<EntryData> textEntries = new();

        private IReadOnlyList<LocalizationTableCollection> collections;

        public ChoiceNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            SetTables();
        }

        public override void SetPorts()
        {
            SetDefault();

            CreatePort(Direction.Input, Port.Capacity.Multi);

            SetOptions();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            evt.menu.AppendAction("Add Choice Port", _ => CreateChoiceEntry());
        }

        private void SetDefault()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            if (data.OptionCount == 0)
            {
                OptionData speakerTable = new(DialogueType.CHOICE_TYPE_000);
                OptionData speakerEntry = new(DialogueType.CHOICE_TYPE_001);

                OptionData textTable = new(DialogueType.CHOICE_TYPE_002);
                OptionData textEntry = new(DialogueType.CHOICE_TYPE_003);

                OptionData message = new(DialogueType.CHOICE_TYPE_004);

                speakerTable.datas.Add(new(GenericData.DataType.String));
                speakerTable.datas.Add(new(GenericData.DataType.Guid));

                speakerEntry.datas.Add(new(GenericData.DataType.String));
                speakerEntry.datas.Add(new(GenericData.DataType.Long));

                textTable.datas.Add(new(GenericData.DataType.String));
                textTable.datas.Add(new(GenericData.DataType.Guid));

                string portName = CreatePortName();

                textEntry.datas.Add(new(portName));
                textEntry.datas.Add(new(GenericData.DataType.String));
                textEntry.datas.Add(new(GenericData.DataType.Long));

                message.datas.Add(new(GenericData.DataType.String));

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
                    case DialogueType.CHOICE_TYPE_000:
                        AddTableField(option.datas, option.type, speakerEntries);
                        break;

                    case DialogueType.CHOICE_TYPE_001:
                        AddSpeakerEntryField(option.datas);
                        break;

                    case DialogueType.CHOICE_TYPE_002:
                        AddTableField(option.datas, option.type, textEntries);
                        break;

                    case DialogueType.CHOICE_TYPE_003:
                        AddChoiceEntryField(option.datas);
                        break;

                    case DialogueType.CHOICE_TYPE_004:
                        AddMessageField(option.datas, option.type);
                        break;
                }
            }
        }

        private void MovePort(Port port, int direction)
        {
            VisualElement container = port.parent;

            if (container == null) return;

            int currentIndex = container.IndexOf(port);
            int newIndex = currentIndex + direction;

            if (newIndex < 0 || newIndex >= container.childCount) return;

            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            Undo.RecordObject(so, "Move Port Index");

            int optionIndex = data.GetOptionIndex(_ => _.Count != 0 && _[0].ToString() == port.portName);

            OptionData option = data.GetOption(optionIndex);

            data.RemoveAtOption(optionIndex);
            data.InsertOption(optionIndex + direction, option);

            container.Insert(newIndex, port);

            so.SetNode(GUID, data);

            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
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

                    OptionData optionData = data.GetOption(optionIndex);

                    optionData.datas[0] = new(collections[index].TableCollectionName);
                    optionData.datas[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);

                    data.SetOption(optionIndex, optionData);
                    so.SetNode(GUID, data);

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddTableField(List<GenericData> datas, string type, List<EntryData> entries)
        {
            int index = GetTableIndex(datas[0].ToString(), datas[1].TryGetGuid(out System.Guid guid) ? guid : default);

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
        #region Speaker Entry
        private void ChangedCallback(ChangeEvent<EntryData> evt)
        {
            int index = speakerEntries.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                NodeData data = so.GetNode(GUID);

                int optionIndex = data.GetOptionIndex(DialogueType.CHOICE_TYPE_001, _ => _.Count != 0);

                if (optionIndex != -1)
                {
                    Undo.RecordObject(so, $"Change {DialogueType.CHOICE_TYPE_001}");

                    OptionData option = data.GetOption(optionIndex);

                    option.datas[0] = new(speakerEntries[index].key);
                    option.datas[1] = new(speakerEntries[index].id);

                    data.SetOption(optionIndex, option);
                    so.SetNode(GUID, data);

                    if (index != -1 && evt.target is PopupField<EntryData> dropdown) dropdown.tooltip = speakerEntries[index].tooltip;

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddSpeakerEntryField(List<GenericData> datas)
        {
            int index = speakerEntries.IndexOf(new EntryData(datas[1].TryGetLong(out long result) ? result : 0, datas[0].ToString()));

            PopupField<EntryData> field = new(DialogueType.CHOICE_TYPE_001, speakerEntries, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt));

            extensionContainer.Add(field);

            if (index != -1)
            {
                field.tooltip = speakerEntries[index].tooltip;

                datas[0] = new(speakerEntries[index].key);
                datas[1] = new(speakerEntries[index].id);
            }
        }
        #endregion

        #region Choice Entry
        private void CreateChoiceEntry()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            string portName = CreatePortName();

            Undo.RecordObject(so, $"Create Output {DialogueType.CHOICE_TYPE_003}");

            OptionData option = new(DialogueType.CHOICE_TYPE_003);

            option.datas.Add(new(portName));
            option.datas.Add(new(GenericData.DataType.String));
            option.datas.Add(new(GenericData.DataType.Long));

            AddChoiceEntryField(option.datas);

            data.AddOption(option);

            so.SetNode(GUID, data);

            RefreshExpandedState();
            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<EntryData> evt, string portName)
        {
            int index = textEntries.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                NodeData data = so.GetNode(GUID);

                int optionIndex = data.GetOptionIndex(DialogueType.CHOICE_TYPE_003, _ => _.Count != 0 && _[0].ToString() == portName);

                if (optionIndex != -1)
                {
                    Undo.RecordObject(so, $"Change {DialogueType.CHOICE_TYPE_003}");

                    OptionData option = data.GetOption(optionIndex);

                    option.datas[1] = new(textEntries[index].key);
                    option.datas[2] = new(textEntries[index].id);

                    data.SetOption(optionIndex, option);
                    so.SetNode(GUID, data);

                    if (index != -1 && evt.target is PopupField<EntryData> dropdown) dropdown.tooltip = textEntries[index].tooltip;

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddChoiceEntryField(List<GenericData> datas)
        {
            int index = textEntries.IndexOf(new EntryData(datas[2].TryGetLong(out long result) ? result : 0, datas[1].ToString()));

            Port port = CreatePort(Direction.Output, Port.Capacity.Single, datas[0].ToString());

            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            PopupField<EntryData> field = new(textEntries, index);

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, datas[0].ToString()));

            Button upButton = new(() => MovePort(port, -1)) { text = "▲" };
            Button downButton = new(() => MovePort(port, 1)) { text = "▼" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };

            container.Add(field);
            container.Add(upButton);
            container.Add(downButton);
            container.Add(removeButton);

            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(container);

            if (index != -1)
            {
                field.tooltip = textEntries[index].tooltip;

                datas[1] = new(textEntries[index].key);
                datas[2] = new(textEntries[index].id);
            }
        }
        #endregion

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
        private void MessageChangedCallback(ChangeEvent<string> evt, string type)
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
            TextField field = new(type) { value = datas[0].ToString() };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => MessageChangedCallback(evt, type));

            extensionContainer.Add(field);
        }
        #endregion
    }
}