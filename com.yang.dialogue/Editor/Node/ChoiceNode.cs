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
    /// <summary>
    /// Port Data (Common : Text Entries)
    /// 0 : Key - string
    /// 1 : ID - long
    /// 2 : Hide - bool
    /// N : Condition - string
    /// N + 1 : Value - float, bool
    /// N + 2 : CheckType - enum
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

        private readonly List<string> conditions = new();

        private readonly IReadOnlyList<LocalizationTableCollection> collections;

        private readonly VisualElement textsElement = new();

        public ChoiceNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            tables = window.Tables;
            collections = window.collections;
        }

        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();

            contentContainer[0].Insert(2, textsElement);

            AddTextField("Speaker", "Speaker Text");

            SetOptions();
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
            if (portDatas.Count == 0)
            {
                DialogueSO so = window.SO;

                LocalizedStringTable speakerOverride = so.SpeakerTable;
                LocalizedStringTable textOverride = so.TextTable;

                DataWrapper speakerTable;
                DataWrapper textTable;

                if (speakerOverride == null || speakerOverride.IsEmpty)
                {
                    speakerTable = new(
                        new(GenericData.DataType.String),
                        new(GenericData.DataType.Guid)
                    );
                }
                else
                {
                    TableReference reference = speakerOverride.TableReference;

                    speakerTable = new(
                        new(reference.TableCollectionName),
                        new(reference.TableCollectionNameGuid)
                    );
                }

                if (textOverride == null || textOverride.IsEmpty)
                {
                    textTable = new(
                        new(GenericData.DataType.String),
                        new(GenericData.DataType.Guid)
                    );
                }
                else
                {
                    TableReference reference = textOverride.TableReference;

                    textTable = new(
                        new(reference.TableCollectionName),
                        new(reference.TableCollectionNameGuid)
                    );
                }

                DataWrapper speakerEntry = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Long)
                );

                DataWrapper textEntry = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Long),
                    new(GenericData.DataType.Bool)
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
            KeyConverter.GetKeys(window.SO.Conditions, conditions);

            AddTableField(true);
            AddSpeakerEntryField();

            AddTableField(false);

            AddMessageField();

            for (int i = 0; i < portDatas.Count; i++)
            {
                IReadOnlyList<GenericData> portOptions = portDatas[i].data;

                VisualElement itemContainer = AddTextEntryField(portDatas[i].data);

                Toggle hide = itemContainer.FindParent<Port>().Q<Toggle>("Hide");

                if (portOptions.Count > 3) hide.style.display = DisplayStyle.Flex;
                else hide.style.display = DisplayStyle.None;

                for (int j = 3; j < portOptions.Count; j += 3)
                {
                    string key = portOptions[j].ToString();

                    switch (portOptions[j + 1].Type)
                    {
                        case GenericData.DataType.Float:
                            {
                                float value = portOptions[j + 1].GetFloat();
                                ValueCheckType type = portOptions[j + 2].GetEnum<ValueCheckType>();

                                itemContainer.Add(GetConditionFloatField(key, value, type));
                            }
                            break;

                        case GenericData.DataType.Bool:
                            {
                                bool value = portOptions[j + 1].GetBool();

                                itemContainer.Add(GetConditionBoolField(key, value));
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

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field.style.whiteSpace = WhiteSpace.Normal;
            field.style.minWidth = StyleKeyword.Auto;

            textsElement.Add(field);

            return field;
        }

        private void MovePort(Port port, int direction)
        {
            VisualElement container = port.parent;

            if (container == null) return;

            int currentIndex = container.IndexOf(port);
            int newIndex = currentIndex + direction;

            if (newIndex < 0 || newIndex >= container.childCount) return;

            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Move Port Index");

            (portDatas[currentIndex], portDatas[newIndex]) = (portDatas[newIndex], portDatas[currentIndex]);

            textsElement.Insert(newIndex + 1, textsElement[currentIndex + 1]);
            container.Insert(newIndex, port);

            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        #region Table
        private void AddTableField(bool speaker)
        {
            List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

            int index = GetTableIndex(optionData[0].ToString(), optionData[1].GetGuid());

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
                LocalizationTableCollection collection = collections[index];

                collection.SetEntries(speaker ? speakerEntries : textEntries);

                optionData[0] = new(collection.TableCollectionName);
                optionData[1] = new(collection.TableCollectionNameReference.TableCollectionNameGuid);
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
                PopupField<string> field = evt.target.FindParentInCurrent<PopupField<string>>();

                if (field == null) return;

                DialogueSO so = window.SO;

                bool speaker = field.name == "Speaker Table";
                List<GenericData> tableOptionData = optionDatas[speaker ? 0 : 2].data;

                Undo.RecordObject(so, "Delete Table Option");

                if (speaker)
                {
                    PopupField<EntryData> entryField = extensionContainer.Q<PopupField<EntryData>>("Speaker Entry");

                    if (entryField != null)
                    {
                        List<GenericData> entryOptionData = optionDatas[speaker ? 1 : 3].data;

                        entryField.value = default;

                        entryOptionData[0] = new(GenericData.DataType.String);
                        entryOptionData[1] = new(GenericData.DataType.Long);

                        TextField textField = textsElement.Q<TextField>("Speaker Text");

                        if (textField != null) textField.value = "";

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

                        PopupField<EntryData> entryField = outputContainer[i].Q<PopupField<EntryData>>("Text Entry");

                        if (entryField != null) entryField.value = default;

                        if (textsElement[i + 1] is TextField textField) textField.value = "";
                    }

                    textEntries.Clear();
                }

                field.value = "";

                tableOptionData[0] = new(GenericData.DataType.String);
                tableOptionData[1] = new(GenericData.DataType.Guid);

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

                LocalizationTableCollection collection = collections[index];

                collection.SetEntries(speaker ? speakerEntries : textEntries);

                Undo.RecordObject(so, "Change Table");

                List<GenericData> optionData = optionDatas[speaker ? 0 : 2].data;

                if (optionData[0].ToString() != collection.TableCollectionName)
                {
                    if (speaker)
                    {
                        PopupField<EntryData> entryField = extensionContainer.Q<PopupField<EntryData>>("Speaker Entry");

                        if (entryField != null)
                        {
                            List<GenericData> entryOptionData = optionDatas[speaker ? 1 : 3].data;

                            entryField.value = default;

                            entryOptionData[0] = new(GenericData.DataType.String);
                            entryOptionData[1] = new(GenericData.DataType.Long);

                            TextField textField = textsElement.Q<TextField>("Speaker Text");

                            if (textField != null) textField.value = "";

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

                            PopupField<EntryData> entryField = outputContainer[i].Q<PopupField<EntryData>>("Text Entry");

                            if (entryField != null) entryField.value = default;

                            if (textsElement[i + 1] is TextField textField) textField.value = "";
                        }

                        textEntries.Clear();
                    }
                }

                optionData[0] = new(collection.TableCollectionName);
                optionData[1] = new(collection.TableCollectionNameReference.TableCollectionNameGuid);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
        #endregion

        #region Entry
        private void AddSpeakerEntryField()
        {
            List<GenericData> optionData = optionDatas[1].data;

            int index = speakerEntries.IndexOf(new EntryData(optionData[1].GetLong(), optionData[0].ToString()));

            string name = "Speaker Entry";
            PopupField<EntryData> field = new(name, speakerEntries, index) { name = name };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedSpeakerCallback);
            field.RegisterCallback<KeyDownEvent>(OnEntryKeyDownEvent);

            extensionContainer.Add(field);

            if (index != -1)
            {
                EntryData entryData = speakerEntries[index];

                optionData[0] = new(entryData.key);
                optionData[1] = new(entryData.id);

                TextField textField = textsElement.Q<TextField>("Speaker Text");

                if (textField != null) textField.value = entryData.GetText(window.Language);
            }
        }

        private void OnEntryKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<EntryData> field = evt.target.FindParentInCurrent<PopupField<EntryData>>();

                if (field == null) return;

                DialogueSO so = window.SO;

                List<GenericData> optionData;

                Undo.RecordObject(so, "Delete Entry Option");

                if (field.name == "Speaker Entry")
                {
                    optionData = optionDatas[1].data;

                    TextField textField = textsElement.Q<TextField>("Speaker Text");

                    if (textField != null) textField.value = "";
                }
                else
                {
                    Port port = evt.target.FindParent<Port>();

                    int index = port.parent.IndexOf(port);

                    optionData = portDatas[index].data;

                    if (textsElement[index + 1] is TextField textField) textField.value = "";
                }

                field.value = default;

                optionData[0] = new(GenericData.DataType.String);
                optionData[1] = new(GenericData.DataType.Long);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedSpeakerCallback(ChangeEvent<EntryData> evt)
        {
            int index = speakerEntries.IndexOf(evt.newValue);

            if (index != -1)
            {
                DialogueSO so = window.SO;

                List<GenericData> optionData = optionDatas[1].data;

                Undo.RecordObject(so, "Change Speaker Entry");

                EntryData entryData = speakerEntries[index];

                optionData[0] = new(entryData.key);
                optionData[1] = new(entryData.id);

                TextField textField = textsElement.Q<TextField>("Speaker Text");

                if (textField != null) textField.value = entryData.GetText(window.Language);

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
            int index = textEntries.IndexOf(new EntryData(optionData[1].GetLong(), optionData[0].ToString()));

            Port port = CreateOutputPort();

            TextField textField = AddTextField("Text", "Text Text");

            VisualElement portElement = new();

            VisualElement groupContainer = new();

            VisualElement entryContainer = new();
            VisualElement itemContainer = new();
            VisualElement buttonContainer = new();

            VisualElement line = new();

            portElement.style.flexGrow = 1;
            portElement.style.flexDirection = FlexDirection.Row;
            portElement.style.alignItems = Align.Stretch;

            groupContainer.style.flexGrow = 1;
            groupContainer.style.flexDirection = FlexDirection.Column;
            groupContainer.style.alignItems = Align.Stretch;

            line.style.marginTop = 7;
            line.style.marginBottom = 7;
            line.style.width = 2;
            line.style.backgroundColor = Color.gray;

            entryContainer.style.flexDirection = FlexDirection.Row;
            entryContainer.style.alignItems = Align.Stretch;

            itemContainer.style.flexDirection = FlexDirection.Column;
            itemContainer.style.alignItems = Align.Stretch;

            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.alignItems = Align.FlexEnd;
            buttonContainer.style.alignSelf = Align.FlexEnd;

            string name = "Text Entry";
            PopupField<EntryData> field = new(name, textEntries, index) { name = name };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(ChangedTextCallback);
            field.RegisterCallback<KeyDownEvent>(OnEntryKeyDownEvent);

            Toggle hide = new()
            {
                name = "Hide",
                value = optionData[2].GetBool(),
                tooltip = "Only passes the option if conditions are met."
            };

            hide.RegisterValueChangedCallback(ChangedTextCallback);

            Button createFloatButton = new(() => CreateConditionFloatField(itemContainer)) { text = "F" };
            Button createBoolButton = new(() => CreateConditionBoolField(itemContainer)) { text = "B" };
            Button upButton = new(() => MovePort(port, -1)) { text = "▲" };
            Button downButton = new(() => MovePort(port, 1)) { text = "▼" };
            Button removeButton = new(() =>
            {
                RemovePort(port);

                textsElement.Remove(textField);
            })
            { text = "X" };

            entryContainer.Add(field);
            entryContainer.Add(hide);

            buttonContainer.Add(createFloatButton);
            buttonContainer.Add(createBoolButton);
            buttonContainer.Add(upButton);
            buttonContainer.Add(downButton);
            buttonContainer.Add(removeButton);

            groupContainer.Add(entryContainer);
            groupContainer.Add(itemContainer);
            groupContainer.Add(buttonContainer);

            portElement.Add(groupContainer);
            portElement.Add(line);

            port.style.height = StyleKeyword.Auto;
            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(portElement);

            if (index != -1)
            {
                EntryData entryData = textEntries[index];

                optionData[0] = new(entryData.key);
                optionData[1] = new(entryData.id);

                textField.value = entryData.GetText(window.Language);
            }

            return itemContainer;
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
                int itemIndex = 3 + itemElement.parent.IndexOf(itemElement);

                portDatas[portIndex].data[itemIndex] = new(GenericData.DataType.String);

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

        private void ChangedTextCallback(ChangeEvent<EntryData> evt)
        {
            int index = textEntries.IndexOf(evt.newValue);

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

                if (textField != null) textField.value = entryData.GetText(window.Language);

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

            portDatas[portIndex].data[itemIndex] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
        #endregion

        #region Float
        private void CreateConditionFloatField(VisualElement itemContainer)
        {
            Port port = itemContainer.FindParent<Port>();

            DialogueSO so = window.SO;

            int portIndex = port.parent.IndexOf(port);

            List<GenericData> portData = portDatas[portIndex].data;

            Undo.RecordObject(so, "Add Condition Float Field");

            itemContainer.Add(GetConditionFloatField("", 0, ValueCheckType.Less));

            portData.Add(new(GenericData.DataType.String));
            portData.Add(new(GenericData.DataType.Float));
            portData.Add(new(GenericData.DataType.Enum));

            Toggle hide = port.Q<Toggle>("Hide");

            hide.style.display = DisplayStyle.Flex;

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private VisualElement GetConditionFloatField(string key, float value, ValueCheckType type)
        {
            VisualElement itemElement = new() { name = "Item Element" };

            itemElement.style.flexDirection = FlexDirection.Row;
            itemElement.style.alignItems = Align.Center;

            KeyConverter.GetKeys(window.SO.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new("Float Condition", conditions, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(ChangedConditionCallback);
            field.RegisterCallback<KeyDownEvent>(OnConditionKeyDownEvent);

            FloatField floatField = new() { value = value };

            floatField.style.minWidth = 60;
            floatField.RegisterValueChangedCallback(ChangedCallback);

            EnumField typeField = new(type);

            typeField.style.minWidth = 70;
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
        private void CreateConditionBoolField(VisualElement itemContainer)
        {
            Port port = itemContainer.FindParent<Port>();

            DialogueSO so = window.SO;

            int portIndex = port.parent.IndexOf(port);

            List<GenericData> portData = portDatas[portIndex].data;

            Undo.RecordObject(so, "Add Condition Bool Field");

            itemContainer.Add(GetConditionBoolField("", false));

            portData.Add(new(GenericData.DataType.String));
            portData.Add(new(GenericData.DataType.Bool));
            portData.Add(new(GenericData.DataType.Enum));

            Toggle hide = port.Q<Toggle>("Hide");

            hide.style.display = DisplayStyle.Flex;

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private VisualElement GetConditionBoolField(string key, bool value)
        {
            VisualElement itemElement = new() { name = "Item Element" };

            itemElement.style.flexDirection = FlexDirection.Row;
            itemElement.style.alignItems = Align.Center;

            KeyConverter.GetKeys(window.SO.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new("Bool Condition", conditions, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
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

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(MessageChangedCallback);

            extensionContainer.Add(field);
        }
        #endregion
    }
}