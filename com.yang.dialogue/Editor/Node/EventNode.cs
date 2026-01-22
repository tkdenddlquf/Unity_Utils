using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class EventNode : BaseNode
    {
        public EventNode(DialogueEditorWindow window, string guid) : base(window, guid)
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

            evt.menu.AppendAction("Add Event", _ => CreateEvent(DialogueType.EVENT_TYPE_000));
        }

        private void SetDefault()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            if (data.OptionCount == 0)
            {
                string triggerName = CreateIDName(DialogueType.EVENT_TYPE_000);
                OptionData optionData = new(DialogueType.EVENT_TYPE_000);

                optionData.datas.Add(triggerName);
                optionData.datas.Add(EMPTY_OPTION);

                data.AddOption(optionData);

                so.SetNode(GUID, data);
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            List<EventKeySO> events = so.events;

            foreach (OptionData option in data.GetOptions())
            {
                List<string> datas = option.datas;

                switch (option.type)
                {
                    case DialogueType.EVENT_TYPE_000:
                        int index = EventKeySO.FindIndex(events, datas[1]);

                        AddEvent(datas[0], index);
                        break;
                }
            }
        }

        private void CreateEvent(string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            string id = CreateIDName(type);

            Undo.RecordObject(so, $"Create {type}");

            OptionData newOption = new(type);

            newOption.datas.Add(id);

            switch (type)
            {
                case DialogueType.EVENT_TYPE_000:
                    AddEvent(id, -1);

                    newOption.datas.Add(EMPTY_OPTION);
                    break;
            }

            data.AddOption(newOption);

            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddEvent(string id, int index)
        {
            DialogueSO so = window.SO;
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            PopupField<EventKeySO> dropdown = new(so.events, index);

            dropdown.style.minWidth = ITEM_MIN_WIDTH;
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt => ChangedCallback(evt, id, DialogueType.EVENT_TYPE_000));

            Button removeButton = new(() => RemoveEvent(container, id, DialogueType.EVENT_TYPE_000)) { text = "X" };

            container.Add(dropdown);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void RemoveEvent(VisualElement container, string id, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == id);

            if (optionIndex != -1 && extensionContainer.childCount > 1)
            {
                Undo.RecordObject(so, "Remove Event");

                data.RemoveAtOption(optionIndex);

                so.SetNode(GUID, data);

                extensionContainer.Remove(container);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<EventKeySO> evt, string id, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int index = 1;
            int optionIndex = -1;

            switch (type)
            {
                case DialogueType.EVENT_TYPE_000:
                    optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == id);
                    break;
            }

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, "Change Event Option");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[index] = evt.newValue.key;

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
    }
}