using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class ConditionNode : BaseNode
    {
        public ConditionNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

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

            evt.menu.AppendAction("Add Single Port", _ => CreatePort(DialogueType.CONDITION_TYPE_002));
            evt.menu.AppendAction("Add Multi Port", _ => CreatePort(DialogueType.CONDITION_TYPE_003));
        }

        private void SetDefault()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            if (data.OptionCount == 0)
            {
                string portName = CreatePortName();
                OptionData optionData = new(DialogueType.CONDITION_TYPE_001);

                optionData.datas.Add(portName);

                data.AddOption(optionData);

                so.SetNode(GUID, data);
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            List<ConditionKeySO> conditions = so.conditions;

            foreach (OptionData option in data.GetOptions())
            {
                List<string> datas = option.datas;

                switch (option.type)
                {
                    case DialogueType.CONDITION_TYPE_001:
                        AddDefaultPort(datas[0]);
                        break;

                    case DialogueType.CONDITION_TYPE_002:
                        {
                            int index = ConditionKeySO.FindIndex(conditions, datas[1]);

                            AddSinglePort(datas[0], index);
                        }
                        break;

                    case DialogueType.CONDITION_TYPE_003:
                        string portName = datas[0];
                        VisualElement itemContainer = AddMultiPort(portName);

                        for (int i = 0; i < datas.Count; i++)
                        {
                            int index = ConditionKeySO.FindIndex(conditions, datas[i]);

                            AddDropdown(portName, option.type, itemContainer, index);
                        }
                        break;
                }
            }
        }

        private void CreatePort(string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            string portName = CreatePortName();

            Undo.RecordObject(so, $"Create Output {type}");

            OptionData option = new(type);

            option.datas.Add(portName);

            switch (type)
            {
                case DialogueType.CONDITION_TYPE_002:
                    AddSinglePort(portName, -1);

                    option.datas.Add(EMPTY_OPTION);
                    break;

                case DialogueType.CONDITION_TYPE_003:
                    VisualElement itemContainer = AddMultiPort(portName);

                    option.datas.Add(EMPTY_OPTION);

                    AddDropdown(portName, type, itemContainer, -1);
                    break;
            }

            data.AddOption(option);

            so.SetNode(GUID, data);

            RefreshExpandedState();
            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void MovePort(Port port, int direction)
        {
            VisualElement container = port.parent;

            if (container == null) return;

            int currentIndex = container.IndexOf(port);
            int newIndex = currentIndex + direction;

            if (newIndex < 1 || newIndex >= container.childCount) return;

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

        private void ChangedCallback(ChangeEvent<ConditionKeySO> evt, string portName, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int index = 1;
            int optionIndex = -1;

            switch (type)
            {
                case DialogueType.CONDITION_TYPE_002:
                    optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == portName);
                    break;

                case DialogueType.CONDITION_TYPE_003:
                    if (evt.currentTarget is PopupField<string> dropdown)
                    {
                        VisualElement container = dropdown.parent;

                        index = container.parent.IndexOf(container) + 1;
                        optionIndex = data.GetOptionIndex(type, _ => _.Count > index && _[0] == portName);
                    }
                    break;
            }

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, "Change Port Option");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[index] = evt.newValue.key;

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        #region Default Port
        private void AddDefaultPort(string portName)
        {
            Port port = CreatePort(Direction.Output, Port.Capacity.Single, portName);

            VisualElement container = new();

            container.style.flexGrow = 1;
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            Label label = new("Default");

            label.style.flexGrow = 1;
            label.style.minWidth = ITEM_MIN_WIDTH;
            label.style.alignSelf = Align.Center;

            container.Add(label);

            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(container);
        }
        #endregion

        #region Single Port
        private void AddSinglePort(string portName, int index)
        {
            Port port = CreatePort(Direction.Output, Port.Capacity.Single, portName);

            DialogueSO so = window.SO;
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            PopupField<ConditionKeySO> dropdown = new(so.conditions, index);

            dropdown.style.flexGrow = 1;
            dropdown.style.minWidth = ITEM_MIN_WIDTH;
            dropdown.RegisterValueChangedCallback(evt => ChangedCallback(evt, portName, DialogueType.CONDITION_TYPE_002));

            Button upButton = new(() => MovePort(port, -1)) { text = "▲" };
            Button downButton = new(() => MovePort(port, 1)) { text = "▼" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };

            container.Add(dropdown);
            container.Add(upButton);
            container.Add(downButton);
            container.Add(removeButton);

            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(container);
        }
        #endregion

        #region Multi Port
        private VisualElement AddMultiPort(string portName)
        {
            Port port = CreatePort(Direction.Output, Port.Capacity.Single, portName);

            DialogueSO so = window.SO;
            VisualElement container = new();

            VisualElement groupContainer = new();

            VisualElement itemContainer = new();
            VisualElement buttonContainer = new();

            VisualElement line = new();

            container.style.flexGrow = 1;
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Stretch;

            groupContainer.style.flexGrow = 1;
            groupContainer.style.flexDirection = FlexDirection.Column;
            groupContainer.style.alignItems = Align.Stretch;

            line.style.marginTop = 7;
            line.style.marginBottom = 7;
            line.style.width = 2;
            line.style.backgroundColor = UnityEngine.Color.gray;

            itemContainer.style.flexDirection = FlexDirection.Column;
            itemContainer.style.alignItems = Align.Stretch;

            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.alignItems = Align.FlexEnd;
            buttonContainer.style.alignSelf = Align.FlexEnd;

            Button createButton = new(() => CreateDropdown(portName, DialogueType.CONDITION_TYPE_003, itemContainer)) { text = "+" };
            Button upButton = new(() => MovePort(port, -1)) { text = "▲" };
            Button downButton = new(() => MovePort(port, 1)) { text = "▼" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };

            buttonContainer.Add(createButton);
            buttonContainer.Add(upButton);
            buttonContainer.Add(downButton);
            buttonContainer.Add(removeButton);

            groupContainer.Add(itemContainer);
            groupContainer.Add(buttonContainer);

            container.Add(groupContainer);
            container.Add(line);

            port.style.height = StyleKeyword.Auto;
            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(container);

            return itemContainer;
        }

        private void CreateDropdown(string portName, string type, VisualElement itemContainer)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == portName);

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, $"Add {type} Option");

                OptionData optionData = data.GetOption(optionIndex);

                AddDropdown(portName, type,itemContainer, -1);

                optionData.datas.Add(EMPTY_OPTION);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void AddDropdown(string portName, string type, VisualElement itemContainer, int index)
        {
            DialogueSO so = window.SO;

            VisualElement dropdownContainer = new();

            dropdownContainer.style.flexDirection = FlexDirection.Row;
            dropdownContainer.style.alignItems = Align.Center;

            PopupField<ConditionKeySO> dropdown = new(so.conditions, index);

            dropdown.style.minWidth = ITEM_MIN_WIDTH;
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt => ChangedCallback(evt, portName, type));

            Button remove = new(() => RemoveDropdown(portName, type, dropdownContainer)) { text = "-" };

            dropdownContainer.Add(dropdown);
            dropdownContainer.Add(remove);

            itemContainer.Add(dropdownContainer);
        }

        private void RemoveDropdown(string portName, string type, VisualElement dropdownContainer)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int index = data.GetOptionIndex(type, _ => _.Count > 1 && _[0] == portName);

            VisualElement itemContainer = dropdownContainer.parent;

            if (index != -1 && itemContainer.childCount > 1)
            {
                Undo.RecordObject(so, $"Remove {type} Option");

                OptionData optionData = data.GetOption(index);

                int containerIndex = itemContainer.IndexOf(dropdownContainer);

                itemContainer.RemoveAt(containerIndex);
                optionData.datas.RemoveAt(containerIndex + 1);

                data.SetOption(index, optionData);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
        #endregion
    }
}