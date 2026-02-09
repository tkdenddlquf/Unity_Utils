using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class WaitNode : BaseNode
    {
        private readonly List<string> events = new();

        public WaitNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

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
                string id = CreateID(DialogueType.WAIT_TYPE_000);
                OptionData optionData = new(DialogueType.WAIT_TYPE_000);

                optionData.datas.Add(id);
                optionData.datas.Add(EMPTY_OPTION);
                optionData.datas.Add(EMPTY_OPTION);

                data.AddOption(optionData);

                so.SetNode(GUID, data);
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            OptionData option = data.GetOption(0);

            List<string> datas = option.datas;

            Enum.TryParse(datas[1], out WaitType waitType);

            EnumField typeField = GetTypeField(waitType, option.type);
            FloatField secondsField = GetSecondsField(datas[2], option.type);
            PopupField<string> eventField = GetEventField(so.Events, datas[2], option.type);

            extensionContainer.Add(typeField);
            extensionContainer.Add(secondsField);
            extensionContainer.Add(eventField);

            SetDisplaySeconds(waitType);
        }

        private void SetDisplaySeconds(WaitType waitType)
        {
            switch (waitType)
            {
                case WaitType.Notify:
                    extensionContainer[1].style.display = DisplayStyle.None;
                    extensionContainer[2].style.display = DisplayStyle.Flex;
                    break;

                case WaitType.Seconds:
                    extensionContainer[1].style.display = DisplayStyle.Flex;
                    extensionContainer[2].style.display = DisplayStyle.None;
                    break;
            }
        }

        private EnumField GetTypeField(WaitType waitType, string type)
        {
            EnumField field = new("Type", waitType);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, type));

            return field;
        }

        private FloatField GetSecondsField(string data, string type)
        {
            FloatField field = new("Seconds");

            float.TryParse(data, out float value);

            field.value = value;

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, type));

            return field;
        }

        private PopupField<string> GetEventField(IEventMarker marker, string data, string type)
        {
            KeyConverter.GetKeys(marker, events);

            int index = events.IndexOf(data);

            PopupField<string> field = new("Event", events, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, type));

            return field;
        }

        private void ChangedCallback(ChangeEvent<Enum> evt, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0);

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, "Change Wait Option");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[1] = evt.newValue.ToString();

                SetDisplaySeconds((WaitType)evt.newValue);

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<float> evt, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0);

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, "Change Wait Second");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[2] = evt.newValue.ToString();

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<string> evt, string type)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            int optionIndex = data.GetOptionIndex(type, _ => _.Count != 0);

            if (optionIndex != -1)
            {
                Undo.RecordObject(so, "Change Wait Event");

                OptionData optionData = data.GetOption(optionIndex);

                optionData.datas[2] = evt.newValue;

                data.SetOption(optionIndex, optionData);
                so.SetNode(GUID, data);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
    }
}