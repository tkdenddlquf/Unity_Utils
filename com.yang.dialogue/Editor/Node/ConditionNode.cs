using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class ConditionNode : BaseNode
    {
        // portDatas: default port followed by condition ports containing (field ID, value, comparison) triples.
        private readonly List<string> conditions = new();

        public ConditionNode(DialogueEditorWindow window, string guid) : base(window, guid) { }

        public override void SetPorts()
        {
            Button add = new(CreateConditionBox) { text = "+", tooltip = "Add condition branch" };
            add.style.width = 22;
            add.style.height = 20;
            titleButtonContainer.Add(add);
            SetDefault();
            CreateInputPort();
            SetOptions();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            DropdownMenu menu = evt.menu;
            menu.AppendAction("Add Condition Box", _ => CreateConditionBox());
            menu.AppendSeparator();
        }

        private void SetDefault()
        {
            if (portDatas.Count == 0) portDatas.Add(new());
        }

        private void SetOptions()
        {
            conditions.Clear();
            AddDefaultCondition();

            for (int i = 1; i < portDatas.Count; i++)
            {
                IReadOnlyList<GenericData> portOptions = portDatas[i].data;
                VisualElement itemContainer = AddConditionBox();

                for (int j = 0; j < portOptions.Count; j += 3)
                {
                    int fieldId = portOptions[j].GetInt();

                    switch (portOptions[j + 1].Type)
                    {
                        case GenericData.DataType.Float:
                            {
                                float value = portOptions[j + 1].GetFloat();
                                ValueCheckType type = portOptions[j + 2].GetEnum<ValueCheckType>();

                                itemContainer.Add(GetConditionFloatField(fieldId, value, type));
                            }
                            break;
                        case GenericData.DataType.Bool:
                            {
                                bool value = portOptions[j + 1].GetBool();
                                itemContainer.Add(GetConditionBoolField(fieldId, value));
                            }
                            break;
                    }
                }
            }
        }

        private void SwapPort(int a, int b)
        {
            DialogueSO so = window.SO;
            Undo.RecordObject(so, "Reorder Condition Port");
            (portDatas[a], portDatas[b]) = (portDatas[b], portDatas[a]);
            outputContainer.Insert(a, outputContainer[b]);
            SwapPortLinks(a, b);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private void AddDefaultCondition()
        {
            Port port = CreateOutputPort();
            VisualElement container = new();

            container.AddToClassList("dlg-row");
            container.style.flexGrow = 1;
            Label label = new("Default");
            label.style.flexGrow = 1;
            label.style.minWidth = ITEM_MIN_WIDTH;
            label.style.alignSelf = Align.Center;
            container.Add(label);
            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(container);
        }

        #region Condition
        private void CreateConditionBox()
        {
            DialogueSO so = window.SO;
            Undo.RecordObject(so, "Create Condition Box");
            AddConditionBox();
            portDatas.Add(new() { data = new() });
            RefreshExpandedState();
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private VisualElement AddConditionBox()
        {
            Port port = CreateOutputPort(false);
            VisualElement portElement = new();
            VisualElement groupContainer = new();
            VisualElement itemContainer = new();
            VisualElement buttonContainer = new();
            VisualElement line = new();

            portElement.AddToClassList("dlg-port-row");
            groupContainer.AddToClassList("dlg-port-col");
            itemContainer.AddToClassList("dlg-port-items");
            buttonContainer.AddToClassList("dlg-port-buttons");
            line.AddToClassList("dlg-port-line");
            Button createVariableButton = new(() =>
                DialogueVariableSchemaUtility.ShowMenu((fieldId, type) =>
                    CreateConditionField(itemContainer, fieldId, type))) { text = "+" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };
            buttonContainer.Add(createVariableButton);
            buttonContainer.Add(removeButton);
            groupContainer.Add(itemContainer);
            groupContainer.Add(buttonContainer);
            portElement.Add(RowDrag.CreateHandle(port, 1, SwapPort, () => RefreshPorts()));
            portElement.Add(groupContainer);
            portElement.Add(line);
            RegisterPortJump(line, port);
            port.style.height = StyleKeyword.Auto;
            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(portElement);

            return itemContainer;
        }

        private void CreateConditionField(VisualElement itemContainer, int fieldId, System.Type type)
        {
            if (type == typeof(float)) CreateConditionFloatField(itemContainer, fieldId);
            else if (type == typeof(bool)) CreateConditionBoolField(itemContainer, fieldId);
        }

        private void RemoveConditionField(VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            VisualElement itemContainer = itemElement.parent;
            Port port = itemContainer.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = itemContainer.IndexOf(itemElement);

            if (itemIndex != -1)
            {
                Undo.RecordObject(so, "Remove Condition Field");
                itemContainer.Remove(itemElement);
                List<GenericData> portData = portDatas[portIndex].data;
                portData.RemoveRange(itemIndex * 3, 3);
                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<string> field = evt.target.FindParentInCurrent<PopupField<string>>();

                if (field == null) return;

                DialogueSO so = window.SO;
                VisualElement itemElement = field.FindParent<VisualElement>("Item Element");
                Port port = itemElement.FindParent<Port>();

                Undo.RecordObject(so, "Delete Condition Option");
                field.value = "";
                int portIndex = port.parent.IndexOf(port);
                int itemIndex = itemElement.parent.IndexOf(itemElement) * 3;

                portDatas[portIndex].data[itemIndex] = new(GenericData.DataType.Int);
                EditorUtility.SetDirty(so);
                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;
            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");
            Port port = itemElement.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = itemElement.parent.IndexOf(itemElement) * 3;

            Undo.RecordObject(so, "Change Condition Option");
            List<GenericData> data = portDatas[portIndex].data;
            int fieldId = DialogueVariableSchemaUtility.GetFieldId(evt.newValue);
            System.Type selectedType = DialogueVariableSchemaUtility.GetValueType(fieldId);
            GenericData.DataType currentType = data[itemIndex + 1].Type;

            if (selectedType == typeof(float) && currentType != GenericData.DataType.Float)
            {
                data[itemIndex] = new GenericData(fieldId);
                data[itemIndex + 1] = new GenericData(0f);
                data[itemIndex + 2] = new GenericData(ValueCheckType.Less);
                ReplaceConditionRow(itemElement, itemIndex / 3, GetConditionFloatField(fieldId, 0f, ValueCheckType.Less));
            }
            else if (selectedType == typeof(bool) && currentType != GenericData.DataType.Bool)
            {
                data[itemIndex] = new GenericData(fieldId);
                data[itemIndex + 1] = new GenericData(false);
                data[itemIndex + 2] = new GenericData(GenericData.DataType.Enum);
                ReplaceConditionRow(itemElement, itemIndex / 3, GetConditionBoolField(fieldId, false));
            }
            else data[itemIndex] = new(fieldId);

            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private static void ReplaceConditionRow(VisualElement oldRow, int index, VisualElement newRow)
        {
            VisualElement parent = oldRow.parent;
            parent.Remove(oldRow);
            parent.Insert(index, newRow);
        }
        #endregion

        #region Float
        private void CreateConditionFloatField(VisualElement itemContainer, int fieldId)
        {
            Port port = itemContainer.FindParent<Port>();
            DialogueSO so = window.SO;
            int portIndex = port.parent.IndexOf(port);
            List<GenericData> portData = portDatas[portIndex].data;

            Undo.RecordObject(so, "Add Condition Float Field");
            itemContainer.Add(GetConditionFloatField(fieldId, 0, ValueCheckType.Less));
            portData.Add(new(fieldId));
            portData.Add(new(GenericData.DataType.Float));
            portData.Add(new(GenericData.DataType.Enum));
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private VisualElement GetConditionFloatField(int fieldId, float value, ValueCheckType type)
        {
            VisualElement itemElement = new() { name = "Item Element" };
            itemElement.AddToClassList("dlg-row");
            DialogueVariableSchemaUtility.GetKeys(conditions);
            int index = conditions.IndexOf(DialogueVariableSchemaUtility.GetLabel(fieldId));
            PopupField<string> field = new("Variable", conditions, index);

            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
            FloatField floatField = new() { value = value };
            floatField.AddToClassList("dlg-num");
            floatField.RegisterValueChangedCallback(ChangedCallback);
            EnumField typeField = new(type);
            typeField.AddToClassList("dlg-enum");
            typeField.RegisterValueChangedCallback(ChangedCallback);
            Button remove = new(() => RemoveConditionField(itemElement)) { text = "-" };
            itemElement.Add(field);
            itemElement.Add(floatField);
            itemElement.Add(typeField);
            itemElement.Add(remove);

            return itemElement;
        }

        private void ChangedCallback(ChangeEvent<float> evt)
        {
            DialogueSO so = window.SO;
            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");
            Port port = itemElement.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = itemElement.parent.IndexOf(itemElement) * 3 + 1;

            Undo.RecordObject(so, "Change Condition Float Option");
            portDatas[portIndex].data[itemIndex] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<System.Enum> evt)
        {
            DialogueSO so = window.SO;
            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");
            Port port = itemElement.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = itemElement.parent.IndexOf(itemElement) * 3 + 2;

            Undo.RecordObject(so, "Change Condition Type Option");
            portDatas[portIndex].data[itemIndex] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }
        #endregion

        #region Bool
        private void CreateConditionBoolField(VisualElement itemContainer, int fieldId)
        {
            Port port = itemContainer.FindParent<Port>();
            DialogueSO so = window.SO;
            int portIndex = port.parent.IndexOf(port);
            List<GenericData> portData = portDatas[portIndex].data;

            Undo.RecordObject(so, "Add Condition Bool Field");
            itemContainer.Add(GetConditionBoolField(fieldId, false));
            portData.Add(new(fieldId));
            portData.Add(new(GenericData.DataType.Bool));
            portData.Add(new(GenericData.DataType.Enum));
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private VisualElement GetConditionBoolField(int fieldId, bool value)
        {
            VisualElement itemElement = new() { name = "Item Element" };
            itemElement.AddToClassList("dlg-row");
            DialogueVariableSchemaUtility.GetKeys(conditions);
            int index = conditions.IndexOf(DialogueVariableSchemaUtility.GetLabel(fieldId));
            PopupField<string> field = new("Variable", conditions, index);

            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
            Toggle toggle = new() { value = value };
            toggle.RegisterValueChangedCallback(ChangedCallback);
            Button remove = new(() => RemoveConditionField(itemElement)) { text = "-" };
            itemElement.Add(field);
            itemElement.Add(toggle);
            itemElement.Add(remove);

            return itemElement;
        }

        private void ChangedCallback(ChangeEvent<bool> evt)
        {
            DialogueSO so = window.SO;
            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");
            Port port = itemElement.FindParent<Port>();
            int portIndex = port.parent.IndexOf(port);
            int itemIndex = itemElement.parent.IndexOf(itemElement) * 3 + 1;

            Undo.RecordObject(so, "Change Condition Bool Option");
            portDatas[portIndex].data[itemIndex] = new(evt.newValue);
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }
        #endregion
    }
}
