using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (0 : Text Entries)
    /// 0 : Key - string
    /// 1 : ID - long
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
    /// Option Data (3 : Message)
    /// 0 : Text - string
    /// </summary>
    public class ChoiceNode : BaseNode
    {
        private readonly List<string> tables;
        private readonly List<EntryData> speakerEntries = new();
        private readonly List<EntryData> textEntries = new();

        private readonly IReadOnlyList<LocalizationTableCollection> collections;

        public ChoiceNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            tables = window.Tables;
            collections = window.collections;
        }

        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();

            SetOptions();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            evt.menu.AppendAction("Add Choice Port", _ => CreateChoiceEntry());
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
                portDatas.Add(textEntry);

                optionDatas.Add(message);
            }
        }

        private void SetOptions()
        {
            AddTableField(speakerEntries, true);

            AddSpeakerEntryField();

            AddTableField(textEntries, false);

            AddMessageField();

            for (int i = 0; i < portDatas.Count; i++) AddChoiceEntryField(portDatas[i].data);
        }

        private void MovePort(Port port, int direction)
        {
            VisualElement container = port.parent;

            if (container == null) return;

            int currentIndex = container.IndexOf(port);
            int newIndex = currentIndex + direction;

            if (newIndex < 1 || newIndex >= container.childCount) return;

            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Move Port Index");

            (portDatas[currentIndex], portDatas[newIndex]) = (portDatas[newIndex], portDatas[currentIndex]);

            container.Insert(newIndex, port);

            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        #region Table
        private void ChangedCallback(ChangeEvent<string> evt, List<EntryData> entries, bool speaker)
        {
            int index = tables.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;

                SetEntries(collections[index], entries);

                Undo.RecordObject(so, speaker ? "Change Speaker Table" : "Change Text Table");

                List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

                optionData[0] = new(collections[index].TableCollectionName);
                optionData[1] = new(collections[index].TableCollectionNameReference.TableCollectionNameGuid);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void AddTableField(List<EntryData> entries, bool speaker)
        {
            List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

            int index = GetTableIndex(optionData[0].ToString(), optionData[1].TryGetGuid(out System.Guid guid) ? guid : default);

            PopupField<string> field = new(speaker ? "Speaker Table" : "Entry Table", tables, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, entries, speaker));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Delete) field.value = "";
            });

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
        #endregion

        #region Entry
        private void CreateChoiceEntry()
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Choice Entry");

            DataWrapper portOption = new(
                new(GenericData.DataType.String),
                new(GenericData.DataType.Long)
            );

            AddChoiceEntryField(portOption.data);

            portDatas.Add(portOption);

            RefreshExpandedState();
            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<EntryData> evt)
        {
            int index = speakerEntries.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;

                List<GenericData> optionData = optionDatas[0].data;

                Undo.RecordObject(so, "Change Speaker Entry");

                optionData[0] = new(speakerEntries[index].key);
                optionData[1] = new(speakerEntries[index].id);

                if (index != -1 && evt.target is PopupField<EntryData> dropdown) dropdown.tooltip = speakerEntries[index].tooltip;

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<EntryData> evt, Port port)
        {
            int index = textEntries.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;

                int portIndex = port.parent.IndexOf(port);

                Undo.RecordObject(so, "Change Port Option");

                List<GenericData> portData = portDatas[portIndex].data;

                portData[0] = new(textEntries[index].key);
                portData[1] = new(textEntries[index].id);

                if (index != -1 && evt.target is PopupField<EntryData> dropdown) dropdown.tooltip = textEntries[index].tooltip;

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void AddSpeakerEntryField()
        {
            List<GenericData> optionData = optionDatas[1].data;

            int index = speakerEntries.IndexOf(new EntryData(optionData[1].TryGetLong(out long result) ? result : 0, optionData[0].ToString()));

            PopupField<EntryData> field = new("Speaker Entry", speakerEntries, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Delete) field.value = default;
            });

            extensionContainer.Add(field);

            if (index != -1)
            {
                field.tooltip = speakerEntries[index].tooltip;

                optionData[0] = new(speakerEntries[index].key);
                optionData[1] = new(speakerEntries[index].id);
            }
        }

        private void AddChoiceEntryField(List<GenericData> datas)
        {
            int index = textEntries.IndexOf(new EntryData(datas[1].TryGetLong(out long result) ? result : 0, datas[0].ToString()));

            Port port = CreateOutputPort();

            VisualElement portElement = new();

            portElement.style.flexDirection = FlexDirection.Row;
            portElement.style.alignItems = Align.Center;

            PopupField<EntryData> field = new(textEntries, index);

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, port));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Delete) field.value = default;
            });

            Button upButton = new(() => MovePort(port, -1)) { text = "▲" };
            Button downButton = new(() => MovePort(port, 1)) { text = "▼" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };

            portElement.Add(field);
            portElement.Add(upButton);
            portElement.Add(downButton);
            portElement.Add(removeButton);

            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(portElement);

            if (index != -1)
            {
                field.tooltip = textEntries[index].tooltip;

                datas[0] = new(textEntries[index].key);
                datas[1] = new(textEntries[index].id);
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

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(MessageChangedCallback);

            extensionContainer.Add(field);
        }
        #endregion
    }
}