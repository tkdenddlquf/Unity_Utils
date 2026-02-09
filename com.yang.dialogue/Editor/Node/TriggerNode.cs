using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class TriggerNode : BaseNode
    {
        private readonly List<string> conditions = new();

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
                string id = CreateID(DialogueType.TRIGGER_TYPE_000);
                OptionData optionData = new(DialogueType.TRIGGER_TYPE_000);

                optionData.datas.Add(id);
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

            KeyConverter.GetKeys(so.Conditions, conditions);

            foreach (OptionData option in data.GetOptions())
            {
                List<string> datas = option.datas;

                switch (option.type)
                {
                    case DialogueType.TRIGGER_TYPE_000:
                        int index = conditions.IndexOf(datas[1]);

                        AddTriggerField(datas[0], index, bool.Parse(datas[2]));
                        break;
                }
            }
        }

        private void CreateTrigger(string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            string id = CreateID(type);

            Undo.RecordObject(so, $"Create {type}");

            OptionData newOption = new(type);

            newOption.datas.Add(id);

            switch (type)
            {
                case DialogueType.TRIGGER_TYPE_000:
                    newOption.datas.Add(EMPTY_OPTION);
                    newOption.datas.Add(true.ToString());

                    AddTriggerField(id, -1);
                    break;
            }

            data.AddOption(newOption);

            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddTriggerField(string id, int index, bool check = true)
        {
            DialogueSO so = window.SO;
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            Toggle toggle = new() { value = check };

            toggle.RegisterValueChangedCallback(evt => ChangedCallback(evt, id, DialogueType.TRIGGER_TYPE_000));

            KeyConverter.GetKeys(so.Conditions, conditions);

            PopupField<string> field = new(conditions, index);

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, id, DialogueType.TRIGGER_TYPE_000));

            Button removeButton = new(() => RemoveTriggerField(container, id, DialogueType.TRIGGER_TYPE_000)) { text = "X" };

            container.Add(toggle);
            container.Add(field);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void RemoveTriggerField(VisualElement container, string id, string type)
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

        private void ChangedCallback(ChangeEvent<string> evt, string id, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == id);

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, $"Change {type} Option");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[1] = evt.newValue;

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<bool> evt, string id, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == id);

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, "Change Trigger Option");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[2] = evt.newValue.ToString();

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
    }
}