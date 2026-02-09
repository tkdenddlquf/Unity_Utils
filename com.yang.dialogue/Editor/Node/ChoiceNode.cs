using System.Collections.Generic;
using System.ComponentModel;
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

                speakerTable.datas.Add(EMPTY_OPTION);
                speakerTable.datas.Add(EMPTY_OPTION);

                speakerEntry.datas.Add(EMPTY_OPTION);
                speakerEntry.datas.Add(EMPTY_OPTION);

                textTable.datas.Add(EMPTY_OPTION);
                textTable.datas.Add(EMPTY_OPTION);

                string portName = CreatePortName();

                textEntry.datas.Add(portName);
                textEntry.datas.Add(EMPTY_OPTION);
                textEntry.datas.Add(EMPTY_OPTION);

                message.datas.Add(EMPTY_OPTION);

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
                        AddTable(option.datas, option.type, speakerEntries);
                        break;

                    case DialogueType.CHOICE_TYPE_001:
                        AddSpeakerEntry(option.datas);
                        break;

                    case DialogueType.CHOICE_TYPE_002:
                        AddTable(option.datas, option.type, textEntries);
                        break;

                    case DialogueType.CHOICE_TYPE_003:
                        AddChoiceEntry(option.datas);
                        break;

                    case DialogueType.CHOICE_TYPE_004:
                        AddMessage(option.datas, option.type);
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

            int optionIndex = data.GetOptionIndex(_ => _.Count != 0 && _[0] == port.portName);

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
        private void TableChangedCallback(ChangeEvent<string> evt, string type, List<EntryData> entries)
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

                    optionData.datas[0] = collections[index].TableCollectionName;
                    optionData.datas[1] = collections[index].TableCollectionNameReference.TableCollectionNameGuid.ToString();

                    data.SetOption(optionIndex, optionData);
                    so.SetNode(GUID, data);

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddTable(List<string> datas, string type, List<EntryData> entries)
        {
            int index = GetTableIndex(datas[0], datas[1]);

            PopupField<string> dropdown = new(type, tables, index);

            dropdown.labelElement.style.minWidth = StyleKeyword.Auto;
            dropdown.labelElement.style.width = StyleKeyword.Auto;
            
            VisualElement box = dropdown[1];

            box.style.minWidth = ITEM_MIN_WIDTH;

            dropdown.RegisterValueChangedCallback(evt => TableChangedCallback(evt, type, entries));

            extensionContainer.Add(dropdown);

            if (index != -1)
            {
                SetEntries(collections[index], entries);

                datas[0] = collections[index].TableCollectionName;
                datas[1] = collections[index].TableCollectionNameReference.TableCollectionNameGuid.ToString();
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

        private int GetTableIndex(string value, string stringGuid)
        {
            System.Guid.TryParse(stringGuid, out System.Guid guid);

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
        private void SpeakerEntryChangedCallback(ChangeEvent<EntryData> evt)
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

                    option.datas[0] = speakerEntries[index].key;
                    option.datas[1] = speakerEntries[index].id.ToString();

                    data.SetOption(optionIndex, option);
                    so.SetNode(GUID, data);

                    if (index != -1 && evt.target is PopupField<EntryData> dropdown) dropdown.tooltip = speakerEntries[index].tooltip;

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddSpeakerEntry(List<string> datas)
        {
            int index = speakerEntries.IndexOf(new EntryData(datas[1], datas[0]));

            PopupField<EntryData> dropdown = new(DialogueType.CHOICE_TYPE_001, speakerEntries, index);

            dropdown.labelElement.style.minWidth = StyleKeyword.Auto;
            dropdown.labelElement.style.width = StyleKeyword.Auto;

            VisualElement box = dropdown[1];

            box.style.minWidth = ITEM_MIN_WIDTH;

            dropdown.RegisterValueChangedCallback(evt => SpeakerEntryChangedCallback(evt));

            extensionContainer.Add(dropdown);

            if (index != -1)
            {
                dropdown.tooltip = speakerEntries[index].tooltip;

                datas[0] = speakerEntries[index].key;
                datas[1] = speakerEntries[index].id.ToString();
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

            option.datas.Add(portName);
            option.datas.Add(EMPTY_OPTION);
            option.datas.Add(EMPTY_OPTION);

            AddChoiceEntry(option.datas);

            data.AddOption(option);

            so.SetNode(GUID, data);

            RefreshExpandedState();
            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChoiceEntryChangedCallback(ChangeEvent<EntryData> evt, string portName)
        {
            int index = textEntries.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;
                NodeData data = so.GetNode(GUID);

                int optionIndex = data.GetOptionIndex(DialogueType.CHOICE_TYPE_003, _ => _.Count != 0 && _[0] == portName);

                if (optionIndex != -1)
                {
                    Undo.RecordObject(so, $"Change {DialogueType.CHOICE_TYPE_003}");

                    OptionData option = data.GetOption(optionIndex);

                    option.datas[1] = textEntries[index].key;
                    option.datas[2] = textEntries[index].id.ToString();

                    data.SetOption(optionIndex, option);
                    so.SetNode(GUID, data);

                    if (index != -1 && evt.target is PopupField<EntryData> dropdown) dropdown.tooltip = textEntries[index].tooltip;

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddChoiceEntry(List<string> datas)
        {
            int index = textEntries.IndexOf(new EntryData(datas[2], datas[1]));

            AddChoiceEntryContainer(datas[0], index);

            if (index != -1)
            {
                datas[1] = textEntries[index].key;
                datas[2] = textEntries[index].id.ToString();
            }
        }

        private void AddChoiceEntryContainer(string portName, int index)
        {
            Port port = CreatePort(Direction.Output, Port.Capacity.Single, portName);

            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            PopupField<EntryData> dropdown = new(textEntries, index);

            dropdown.style.flexGrow = 1;
            dropdown.style.minWidth = ITEM_MIN_WIDTH;
            dropdown.RegisterValueChangedCallback(evt => ChoiceEntryChangedCallback(evt, portName));

            Button upButton = new(() => MovePort(port, -1)) { text = "▲" };
            Button downButton = new(() => MovePort(port, 1)) { text = "▼" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };

            container.Add(dropdown);
            container.Add(upButton);
            container.Add(downButton);
            container.Add(removeButton);

            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(container);

            if (index != -1) dropdown.tooltip = textEntries[index].tooltip;
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

                option.datas[0] = evt.newValue;

                data.SetOption(optionIndex, option);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void AddMessage(List<string> datas, string type)
        {
            TextField text = new(type) { value = datas[0] };

            text.labelElement.style.minWidth = StyleKeyword.Auto;
            text.labelElement.style.width = StyleKeyword.Auto;

            text.style.minWidth = ITEM_MIN_WIDTH;
            text.RegisterValueChangedCallback(evt => MessageChangedCallback(evt, type));

            extensionContainer.Add(text);
        }
        #endregion
    }
}