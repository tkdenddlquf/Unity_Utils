using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (Unused)
    /// 
    /// Option Data (Float)
    /// 0 : Key - string
    /// 1 : Value - float, bool
    /// 2 : SetterType - enum
    /// 
    /// Option Data (Bool)
    /// 0 : Key - string
    /// 1 : Value - float, bool
    /// </summary>
    public class TriggerNode : BaseNode
    {
        private readonly List<string> conditions = new();

        public TriggerNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();

            SetOptions();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            DropdownMenu menu = evt.menu;

            menu.AppendAction("Add Float Trigger", _ => CreateFloatField());
            menu.AppendAction("Add Bool Trigger", _ => CreateBoolField());
            menu.AppendSeparator();
        }

        private void SetDefault()
        {
            if (portDatas.Count == 0)
            {
                DataWrapper optionData = new(
                    new(GenericData.DataType.String),
                    new(GenericData.DataType.Bool)
                );

                optionDatas.Add(optionData);

                portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            KeyConverter.GetKeys(window.SO.Conditions, conditions);

            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> optionData = optionDatas[i].data;

                string key = optionData[0].ToString();

                switch (optionData[1].Type)
                {
                    case GenericData.DataType.Float:
                        AddFloatField(key, optionData[1].GetFloat(), optionData[2].GetEnum<RunnerValue.SetterType>());
                        break;

                    case GenericData.DataType.Bool:
                        AddBoolField(key, optionData[1].GetBool());
                        break;
                }
            }
        }

        private void ChangedCallback(ChangeEvent<string> evt, VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            int optionIndex = container.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Trigger Option");

            optionDatas[optionIndex].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        #region Float
        private void CreateFloatField()
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Float Trigger");

            DataWrapper optionData = new(
                new(GenericData.DataType.String),
                new(GenericData.DataType.Float),
                new(GenericData.DataType.Enum)
            );

            AddFloatField("", 0, RunnerValue.SetterType.Plus);

            optionDatas.Add(optionData);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddFloatField(string key, float value, RunnerValue.SetterType type)
        {
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            KeyConverter.GetKeys(window.SO.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new("Float Trigger", conditions, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Delete)
                {
                    DialogueSO so = window.SO;

                    Undo.RecordObject(so, "Delete Float Trigger Option");

                    field.value = "";

                    int optionIndex = container.parent.IndexOf(container);

                    optionDatas[optionIndex].data[0] = new(GenericData.DataType.String);

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            });

            FloatField floatField = new() { value = value };

            floatField.style.minWidth = 60;
            floatField.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));

            EnumField typeField = new(type);

            typeField.style.minWidth = 70;
            typeField.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));

            Button removeButton = new(() => RemoveFloatField(container)) { text = "X" };

            container.Add(field);
            container.Add(floatField);
            container.Add(typeField);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void RemoveFloatField(VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            if (container.childCount > 1)
            {
                int optionIndex = container.IndexOf(itemElement);

                Undo.RecordObject(so, "Remove Float Trigger");

                optionDatas.RemoveAt(optionIndex);

                container.Remove(itemElement);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<float> evt, VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            int optionIndex = container.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Float Trigger Option");

            optionDatas[optionIndex].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<System.Enum> evt, VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            int optionIndex = container.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Trigger Option");

            optionDatas[optionIndex].data[2] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
        #endregion

        #region Bool
        private void CreateBoolField()
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Bool Trigger");

            DataWrapper optionData = new(
                new(GenericData.DataType.String),
                new(GenericData.DataType.Bool)
            );

            AddBoolField("", false);

            optionDatas.Add(optionData);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddBoolField(string key, bool value)
        {
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            KeyConverter.GetKeys(window.SO.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new("Bool Trigger", conditions, index);

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Delete)
                {
                    DialogueSO so = window.SO;

                    Undo.RecordObject(so, "Delete Bool Trigger Option");

                    field.value = "";

                    int optionIndex = container.parent.IndexOf(container);

                    optionDatas[optionIndex].data[0] = new(GenericData.DataType.String);

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            });

            Toggle toggle = new() { value = value };

            toggle.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));

            Button removeButton = new(() => RemoveBoolField(container)) { text = "X" };

            container.Add(field);
            container.Add(toggle);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void RemoveBoolField(VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            if (container.childCount > 1)
            {
                int optionIndex = container.IndexOf(itemElement);

                Undo.RecordObject(so, "Remove Bool Trigger");

                optionDatas.RemoveAt(optionIndex);

                container.Remove(itemElement);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<bool> evt, VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            int optionIndex = container.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Bool Trigger Option");

            optionDatas[optionIndex].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
        #endregion
    }
}