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

        /// <summary>Constructs the node from the editor window and guid.</summary>
        public ConditionNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        /// <summary>Builds the input port and condition output boxes.</summary>
        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();

            SetOptions();
        }

        /// <summary>Adds the right-click menu entry for creating a new condition box.</summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            DropdownMenu menu = evt.menu;

            menu.AppendAction("Add Condition Box", _ => CreateConditionBox());
            menu.AppendSeparator();
        }

        /// <summary>Seeds the default port data slot when the node has none yet.</summary>
        private void SetDefault()
        {
            if (portDatas.Count == 0) portDatas.Add(new());
        }

        /// <summary>Builds the default port and rebuilds each condition box's rows from data.</summary>
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

        /// <summary>Reorders two condition ports along with their links.</summary>
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

        /// <summary>Builds the fixed "Default" fallback output port.</summary>
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
        /// <summary>Creates a new empty condition box and its port data entry.</summary>
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

        /// <summary>Builds a condition box port row (item container, add buttons, remove) and returns its item container.</summary>
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

            RegisterPortJump(line, port);

            port.style.height = StyleKeyword.Auto;
            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(portElement);

            return itemContainer;
        }

        /// <summary>Removes a condition row from a box and its 3-slot data group.</summary>
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

        /// <summary>Clears a condition key selection when Delete is pressed.</summary>
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

        /// <summary>Persists a condition key change into the matching port data slot.</summary>
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
        /// <summary>Adds a float condition row and its 3-slot data group to a condition box.</summary>
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

        /// <summary>Builds a float condition row (key dropdown, value, comparison enum, remove button).</summary>
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

        /// <summary>Persists a float condition value change into its port data slot.</summary>
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

        /// <summary>Persists a comparison-type enum change into its port data slot.</summary>
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
        /// <summary>Adds a bool condition row and its 3-slot data group to a condition box.</summary>
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

        /// <summary>Builds a bool condition row (key dropdown, toggle, remove button).</summary>
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

        /// <summary>Persists a bool condition value change into its port data slot.</summary>
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