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

        /// <summary>Creates the trigger node.</summary>
        public TriggerNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        /// <summary>Ensures default data, creates input/output ports, and builds the trigger fields.</summary>
        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();

            SetOptions();
        }

        /// <summary>Adds "Add Float Trigger" and "Add Bool Trigger" entries to the context menu.</summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            DropdownMenu menu = evt.menu;

            menu.AppendAction("Add Float Trigger", _ => CreateFloatField());
            menu.AppendAction("Add Bool Trigger", _ => CreateBoolField());
            menu.AppendSeparator();
        }

        /// <summary>Seeds default option and port data when none exist.</summary>
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

        /// <summary>Loads condition keys and builds a float or bool field per option entry by its data type.</summary>
        private void SetOptions()
        {
            window.GetKeysInto(window.SO.Conditions, conditions);

            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> optionData = optionDatas[i].data;

                string key = optionData[0].ToString();

                switch (optionData[1].Type)
                {
                    case GenericData.DataType.Float:
                        AddFloatField(key, optionData[1].GetFloat(), optionData[2].GetEnum<ValueSetterType>());
                        break;

                    case GenericData.DataType.Bool:
                        AddBoolField(key, optionData[1].GetBool());
                        break;
                }
            }
        }

        /// <summary>Swaps two option entries and their row elements for reordering.</summary>
        private void SwapOption(int a, int b)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Reorder Trigger");

            (optionDatas[a], optionDatas[b]) = (optionDatas[b], optionDatas[a]);

            extensionContainer.Insert(a, extensionContainer[b]);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>On Delete, clears the focused trigger field and its key option data.</summary>
        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<string> field = evt.target.FindParentInCurrent<PopupField<string>>();

                if (field == null) return;

                DialogueSO so = window.SO;

                VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");

                Undo.RecordObject(so, "Delete Trigger Option");

                field.value = "";

                int optionIndex = itemElement.parent.IndexOf(itemElement);

                optionDatas[optionIndex].data[0] = new(GenericData.DataType.String);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        /// <summary>Writes a changed trigger key into the matching option data entry.</summary>
        private void ChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;

            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");

            int optionIndex = itemElement.parent.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Trigger Option");

            optionDatas[optionIndex].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Appends a new float trigger field and its option data with undo support.</summary>
        private void CreateFloatField()
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Float Trigger");

            DataWrapper optionData = new(
                new(GenericData.DataType.String),
                new(GenericData.DataType.Float),
                new(GenericData.DataType.Enum)
            );

            AddFloatField("", 0, ValueSetterType.Plus);

            optionDatas.Add(optionData);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Builds a draggable row with a condition popup, float value, setter-type enum, and remove button.</summary>
        private void AddFloatField(string key, float value, ValueSetterType type)
        {
            VisualElement container = new() { name = "Item Element" };

            container.AddToClassList("dlg-row");

            window.GetKeysInto(window.SO.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new("Float Trigger", conditions, index);

            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);

            FloatField floatField = new() { value = value };

            floatField.AddToClassList("dlg-num");
            floatField.RegisterValueChangedCallback(ChangedCallback);

            EnumField typeField = new(type);

            typeField.AddToClassList("dlg-enum");
            typeField.RegisterValueChangedCallback(ChangedCallback);

            Button removeButton = new(() => RemoveFloatField(container)) { text = "X" };

            container.Add(RowDrag.CreateHandle(container, 0, SwapOption));
            container.Add(field);
            container.Add(floatField);
            container.Add(typeField);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        /// <summary>Removes a float trigger row and its option data, keeping at least one entry.</summary>
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

        /// <summary>Writes a changed float trigger value into the matching option data entry.</summary>
        private void ChangedCallback(ChangeEvent<float> evt)
        {
            DialogueSO so = window.SO;

            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");

            int optionIndex = itemElement.parent.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Float Trigger Option");

            optionDatas[optionIndex].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Writes a changed setter-type enum into the matching option data entry.</summary>
        private void ChangedCallback(ChangeEvent<System.Enum> evt)
        {
            DialogueSO so = window.SO;

            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");

            int optionIndex = itemElement.parent.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Trigger Option");

            optionDatas[optionIndex].data[2] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
        /// <summary>Appends a new bool trigger field and its option data with undo support.</summary>
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

        /// <summary>Builds a draggable row with a condition popup, bool toggle, and remove button.</summary>
        private void AddBoolField(string key, bool value)
        {
            VisualElement container = new() { name = "Item Element" };

            container.AddToClassList("dlg-row");

            window.GetKeysInto(window.SO.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new("Bool Trigger", conditions, index);

            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);

            Toggle toggle = new() { value = value };

            toggle.RegisterValueChangedCallback(ChangedCallback);

            Button removeButton = new(() => RemoveBoolField(container)) { text = "X" };

            container.Add(RowDrag.CreateHandle(container, 0, SwapOption));
            container.Add(field);
            container.Add(toggle);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        /// <summary>Removes a bool trigger row and its option data, keeping at least one entry.</summary>
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

        /// <summary>Writes a changed bool trigger value into the matching option data entry.</summary>
        private void ChangedCallback(ChangeEvent<bool> evt)
        {
            DialogueSO so = window.SO;

            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");

            int optionIndex = itemElement.parent.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Bool Trigger Option");

            optionDatas[optionIndex].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}