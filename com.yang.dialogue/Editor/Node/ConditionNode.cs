using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (0 : Default)
    /// Unused Options
    /// 
    /// Port Data (Common)
    /// N : Condition - string
    /// N + 1 : Value - float, bool
    /// N + 2 : CheckType - enum
    /// 
    /// Option Data (Unused)
    /// </summary>
    public class ConditionNode : BaseNode
    {
        private readonly List<string> conditions = new();

        public ConditionNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        public override void SetPorts()
        {
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
            window.GetKeysInto(window.SO.Conditions, conditions);

            AddDefaultCondition();

            for (int i = 1; i < portDatas.Count; i++)
            {
                IReadOnlyList<GenericData> portOptions = portDatas[i].data;

                VisualElement itemContainer = AddConditionBox();

                for (int j = 0; j < portOptions.Count; j += 3)
                {
                    string key = portOptions[j].ToString();

                    switch (portOptions[j + 1].Type)
                    {
                        case GenericData.DataType.Float:
                            {
                                float value = portOptions[j + 1].GetFloat();
                                ValueCheckType type = portOptions[j + 2].GetEnum<ValueCheckType>();

                                itemContainer.Add(GetConditionFloatField(key, value, type));
                            }
                            break;

                        case GenericData.DataType.Bool:
                            {
                                bool value = portOptions[j + 1].GetBool();

                                itemContainer.Add(GetConditionBoolField(key, value));
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
            Port port = CreateOutputPort();

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

            Button createFloatButton = new(() => CreateConditionFloatField(itemContainer)) { text = "F" };
            Button createBoolButton = new(() => CreateConditionBoolField(itemContainer)) { text = "B" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };

            buttonContainer.Add(createFloatButton);
            buttonContainer.Add(createBoolButton);
            buttonContainer.Add(removeButton);

            groupContainer.Add(itemContainer);
            groupContainer.Add(buttonContainer);

            portElement.Add(RowDrag.CreateHandle(port, 1, SwapPort, () => RefreshPorts()));
            portElement.Add(groupContainer);
            portElement.Add(line);

            port.style.height = StyleKeyword.Auto;
            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(portElement);

            return itemContainer;
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
                int itemIndex = itemElement.parent.IndexOf(itemElement);

                portDatas[portIndex].data[itemIndex] = new(GenericData.DataType.String);

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

            portDatas[portIndex].data[itemIndex] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
        #endregion

        #region Float
        private void CreateConditionFloatField(VisualElement itemContainer)
        {
            Port port = itemContainer.FindParent<Port>();

            DialogueSO so = window.SO;

            int portIndex = port.parent.IndexOf(port);

            List<GenericData> portData = portDatas[portIndex].data;

            Undo.RecordObject(so, "Add Condition Float Field");

            itemContainer.Add(GetConditionFloatField("", 0, ValueCheckType.Less));

            portData.Add(new(GenericData.DataType.String));
            portData.Add(new(GenericData.DataType.Float));
            portData.Add(new(GenericData.DataType.Enum));

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private VisualElement GetConditionFloatField(string key, float value, ValueCheckType type)
        {
            VisualElement itemElement = new() { name = "Item Element" };

            itemElement.AddToClassList("dlg-row");

            window.GetKeysInto(window.SO.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new("Float Condition", conditions, index);

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
        private void CreateConditionBoolField(VisualElement itemContainer)
        {
            Port port = itemContainer.FindParent<Port>();

            DialogueSO so = window.SO;

            int portIndex = port.parent.IndexOf(port);

            List<GenericData> portData = portDatas[portIndex].data;

            Undo.RecordObject(so, "Add Condition Bool Field");

            itemContainer.Add(GetConditionBoolField("", false));

            portData.Add(new(GenericData.DataType.String));
            portData.Add(new(GenericData.DataType.Bool));
            portData.Add(new(GenericData.DataType.Enum));

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private VisualElement GetConditionBoolField(string key, bool value)
        {
            VisualElement itemElement = new() { name = "Item Element" };

            itemElement.AddToClassList("dlg-row");

            window.GetKeysInto(window.SO.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new("Bool Condition", conditions, index);

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