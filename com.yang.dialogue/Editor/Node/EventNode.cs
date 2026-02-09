using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class EventNode : BaseNode
    {
        private readonly List<string> events = new();

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
                string id = CreateID(DialogueType.EVENT_TYPE_000);
                OptionData optionData = new(DialogueType.EVENT_TYPE_000);

                optionData.datas.Add(id);
                optionData.datas.Add(EMPTY_OPTION);

                data.AddOption(optionData);

                so.SetNode(GUID, data);
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            KeyConverter.GetKeys(so.Events, events);

            foreach (OptionData option in data.GetOptions())
            {
                List<string> datas = option.datas;

                switch (option.type)
                {
                    case DialogueType.EVENT_TYPE_000:
                        int index = events.IndexOf(datas[1]);

                        AddEventField(datas[0], index);
                        break;
                }
            }
        }

        private void CreateEvent(string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            string id = CreateID(type);

            Undo.RecordObject(so, $"Create {type}");

            OptionData newOption = new(type);

            newOption.datas.Add(id);

            switch (type)
            {
                case DialogueType.EVENT_TYPE_000:
                    AddEventField(id, -1);

                    newOption.datas.Add(EMPTY_OPTION);
                    break;
            }

            data.AddOption(newOption);

            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddEventField(string id, int index)
        {
            DialogueSO so = window.SO;
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            KeyConverter.GetKeys(so.Events, events);

            PopupField<string> field = new(events, index);

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, id, DialogueType.EVENT_TYPE_000));

            Button removeButton = new(() => RemoveEventContainer(container, id, DialogueType.EVENT_TYPE_000)) { text = "X" };

            container.Add(field);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void RemoveEventContainer(VisualElement container, string id, string type)
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

        private void ChangedCallback(ChangeEvent<string> evt, string id, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0 && _[0] == id);

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, "Change Event Option");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[1] = evt.newValue;

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
    }
}