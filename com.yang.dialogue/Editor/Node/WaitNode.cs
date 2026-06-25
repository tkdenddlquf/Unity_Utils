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
        public enum WaitType
        {
            Notify,
            Seconds,
        }

        /// <summary>Creates the wait node.</summary>
        public WaitNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        /// <summary>Ensures default data exists, then creates the input and output ports.</summary>
        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();
        }

        /// <summary>Builds the wait type, seconds, and reason fields in the extension container.</summary>
        protected override void BuildExtension() => SetOptions();

        /// <summary>Seeds default option and port data when none exist.</summary>
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

        /// <summary>Builds the type, seconds, and reason fields from option data and sets initial visibility.</summary>
        private void SetOptions()
        {
            List<GenericData> optionData = optionDatas[0].data;

            if (optionData[0].TryGetEnum(out WaitType eResult))
            {
                EnumField typeField = GetTypeField(eResult);
                FloatField secondsField = GetSecondsField(optionData[1].GetFloat());
                TextField eventField = GetReasonField(optionData[1].ToString());

                extensionContainer.Add(typeField);
                extensionContainer.Add(secondsField);
                extensionContainer.Add(eventField);

                SetDisplaySeconds(eResult, true);
            }
        }

        /// <summary>Toggles the seconds and reason fields per wait type and refreshes the shown field's value.</summary>
        private void SetDisplaySeconds(WaitType waitType, bool setElementValue)
        {
            TextField reasonField = extensionContainer.Q<TextField>();
            FloatField secondsField = extensionContainer.Q<FloatField>();

            switch (waitType)
            {
                case WaitType.Notify:
                    secondsField.style.display = DisplayStyle.None;

                    if (setElementValue)
                    {
                        string currentReason = reasonField.value;

                        reasonField.SetValueWithoutNotify("");
                        reasonField.value = currentReason;
                    }
                    else
                    {
                        string value = optionDatas[0].data[1].ToString();

                        reasonField.SetValueWithoutNotify(value);
                    }

                    reasonField.style.display = DisplayStyle.Flex;
                    break;

                case WaitType.Seconds:
                    reasonField.style.display = DisplayStyle.None;

                    if (setElementValue)
                    {
                        float currentSeconds = secondsField.value;

                        secondsField.SetValueWithoutNotify(currentSeconds - 1);
                        secondsField.value = currentSeconds;
                    }
                    else
                    {
                        float value = optionDatas[0].data[1].GetFloat();

                        secondsField.SetValueWithoutNotify(value);
                    }

                    secondsField.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        /// <summary>Creates the wait-type enum field bound to the change callback.</summary>
        private EnumField GetTypeField(WaitType waitType)
        {
            EnumField field = new("Type", waitType);

            field.AddToClassList("dlg-field");

            field.RegisterValueChangedCallback(ChangedCallback);

            return field;
        }

        /// <summary>Creates the seconds float field bound to the change callback.</summary>
        private FloatField GetSecondsField(float data)
        {
            FloatField field = new("Seconds") { value = data };

            field.AddToClassList("dlg-field");

            field.RegisterValueChangedCallback(ChangedCallback);

            return field;
        }

        /// <summary>Creates the reason text field bound to the change callback.</summary>
        private TextField GetReasonField(string data)
        {
            TextField field = new("Reason") { value = data };

            field.AddToClassList("dlg-field");

            field.RegisterValueChangedCallback(ChangedCallback);

            return field;
        }

        /// <summary>Handles a wait-type change, updating option data and refreshing field visibility.</summary>
        private void ChangedCallback(ChangeEvent<Enum> evt)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Wait Option");

            WaitType type = (WaitType)evt.newValue;

            optionDatas[0].data[0] = new(type);

            SetDisplaySeconds(type, true);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Handles a seconds change, writing the new value into option data.</summary>
        private void ChangedCallback(ChangeEvent<float> evt)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Change Wait Second");

            optionDatas[0].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Handles a reason change, writing the new value into option data.</summary>
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