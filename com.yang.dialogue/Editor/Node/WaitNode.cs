using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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
        public enum WaitType
        {
            Notify,
            Seconds,
        }

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
            if (portDatas.Count == 0)
            {
                DataWrapper optionData = new(
                    new(WaitType.Notify),
                    new(GenericData.DataType.String)
                );

                optionDatas.Add(optionData);

                portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            List<GenericData> optionData = optionDatas[0].data;

            if (optionData[0].TryGetEnum(out WaitType eResult))
            {
                EnumField typeField = GetTypeField(eResult);
                FloatField secondsField = GetSecondsField(optionData[1].TryGetFloat(out float fResult) ? fResult : 0);
                TextField eventField = GetReasonField(optionData[1].ToString());

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

                    TextField reasonField = extensionContainer[2] as TextField;

                    string currentReason = reasonField.value;

                    reasonField.SetValueWithoutNotify("");
                    reasonField.value = currentReason;
                    reasonField.style.display = DisplayStyle.Flex;
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

        private TextField GetReasonField(string data)
        {
            TextField field = new("Reason") { value = data };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);

            return field;
        }

        private void ChangedCallback(ChangeEvent<Enum> evt)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Wait Option");

            WaitType type = (WaitType)evt.newValue;

            optionDatas[0].data[0] = new(type);

            SetDisplaySeconds(type);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<float> evt)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Wait Second");

            optionDatas[0].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Wait Event");

            optionDatas[0].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}