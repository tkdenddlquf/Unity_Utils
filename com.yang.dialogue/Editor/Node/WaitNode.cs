using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (Unused)
    /// 
    /// Option Data (Unique)
    /// 0 : WaitType - enum
    /// 1 : Value - float, string
    /// </summary>
    public class WaitNode : BaseNode
    {
        private readonly List<string> events = new();

        public WaitNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();

            SetOptions();
        }

        private void SetDefault()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            if (data.portDatas.Count == 0)
            {
                DataWrapper optionData = new(
                    new(WaitType.Notify),
                    new(GenericData.DataType.String)
                );

                data.optionDatas.Add(optionData);

                data.portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            List<GenericData> datas = data.optionDatas[0].data;

            if (datas[0].TryGetEnum(out WaitType eResult))
            {
                EnumField typeField = GetTypeField(eResult);
                FloatField secondsField = GetSecondsField(datas[1].TryGetFloat(out float fResult) ? fResult : 0);
                PopupField<string> eventField = GetEventField(so.Events, datas[1].ToString());

                extensionContainer.Add(typeField);
                extensionContainer.Add(secondsField);
                extensionContainer.Add(eventField);

                SetDisplaySeconds(eResult);
            }
        }

        private void SetDisplaySeconds(WaitType waitType)
        {
            switch (waitType)
            {
                case WaitType.Notify:
                    extensionContainer[1].style.display = DisplayStyle.None;

                    PopupField<string> eventField = extensionContainer[2] as PopupField<string>;

                    string currentEvent = eventField.value;

                    eventField.SetValueWithoutNotify("");
                    eventField.value = currentEvent;
                    eventField.style.display = DisplayStyle.Flex;
                    break;

                case WaitType.Seconds:
                    extensionContainer[2].style.display = DisplayStyle.None;

                    FloatField secondsField = extensionContainer[1] as FloatField;

                    float currentSeconds = secondsField.value;

                    secondsField.SetValueWithoutNotify(currentSeconds - 1);
                    secondsField.value = currentSeconds;
                    secondsField.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        private EnumField GetTypeField(WaitType waitType)
        {
            EnumField field = new("Type", waitType);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);

            return field;
        }

        private FloatField GetSecondsField(float data)
        {
            FloatField field = new("Seconds") { value = data };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);

            return field;
        }

        private PopupField<string> GetEventField(IEventMarker marker, string data)
        {
            KeyConverter.GetKeys(marker, events);

            int index = events.IndexOf(data);

            PopupField<string> field = new("Event", events, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);

            return field;
        }

        private void ChangedCallback(ChangeEvent<Enum> evt)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            Undo.RecordObject(so, "Change Wait Option");

            WaitType type = (WaitType)evt.newValue;

            data.optionDatas[0].data[0] = new(type);

            SetDisplaySeconds(type);

            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<float> evt)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            Undo.RecordObject(so, "Change Wait Second");

            data.optionDatas[0].data[1] = new(evt.newValue);

            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            Undo.RecordObject(so, "Change Wait Event");

            data.optionDatas[0].data[1] = new(evt.newValue);

            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}