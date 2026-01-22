using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class TriggerNode : BaseNode
    {
        public TriggerNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        public override void SetPorts()
        {
            SetDefault();

            CreatePort(Direction.Input, Port.Capacity.Multi);
            CreatePort(Direction.Output, Port.Capacity.Single);

            SetOptions();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            evt.menu.AppendAction("Add Trigger", _ => CreateTrigger(DialogueType.TRIGGER_TYPE_000));
        }

        private void SetDefault()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            if (data.OptionCount == 0)
            {
                string triggerName = CreateIDName(DialogueType.TRIGGER_TYPE_000);
                OptionData optionData = new(DialogueType.TRIGGER_TYPE_000);

                optionData.datas.Add(triggerName);
                optionData.datas.Add(EMPTY_OPTION);
                optionData.datas.Add(true.ToString());

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
                    case DialogueType.TRIGGER_TYPE_000:
                        int index = ConditionKeySO.FindIndex(conditions, datas[1]);

                        AddTrigger(datas[0], index, bool.Parse(datas[2]));
                        break;
                }
            }
        }

        private void CreateTrigger(string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            string id = CreateIDName(type);

            Undo.RecordObject(so, $"Create {type}");

            OptionData newOption = new(type);

            newOption.datas.Add(id);

            switch (type)
            {
                case DialogueType.TRIGGER_TYPE_000:
                    newOption.datas.Add(EMPTY_OPTION);
                    newOption.datas.Add(true.ToString());

                    AddTrigger(id, -1);
                    break;
            }

            data.AddOption(newOption);

            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddTrigger(string id, int index, bool check = true)
        {
            DialogueSO so = window.SO;
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            Toggle toggle = new() { value = check };

            toggle.RegisterValueChangedCallback(evt => ToggleCallback(evt, id, DialogueType.TRIGGER_TYPE_000));

            PopupField<ConditionKeySO> dropdown = new(so.conditions, index);

            dropdown.style.minWidth = ITEM_MIN_WIDTH;
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt => ChangedCallback(evt, id, DialogueType.TRIGGER_TYPE_000));

            Button removeButton = new(() => RemoveTrigger(container, id, DialogueType.TRIGGER_TYPE_000)) { text = "X" };

            container.Add(toggle);
            container.Add(dropdown);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void RemoveTrigger(VisualElement container, string id, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == id);

            if (optionIndex != -1 && extensionContainer.childCount > 1)
            {
                Undo.RecordObject(so, $"Remove {type}");

                data.RemoveAtOption(optionIndex);

                so.SetNode(GUID, data);

                extensionContainer.Remove(container);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<ConditionKeySO> evt, string id, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int index = 1;
            int optionIndex = -1;

            switch (type)
            {
                case DialogueType.TRIGGER_TYPE_000:
                    optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == id);
                    break;
            }

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, $"Change {type} Option");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[index] = evt.newValue.key;

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ToggleCallback(ChangeEvent<bool> evt, string id, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int index = 2;
            int optionIndex = -1;

            switch (type)
            {
                case DialogueType.TRIGGER_TYPE_000:
                    optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == id);
                    break;
            }

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, "Change Trigger Option");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[index] = evt.newValue.ToString();

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
    }
}