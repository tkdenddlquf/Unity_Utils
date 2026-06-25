using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (Unused)
    /// 
    /// Option Data (Common)
    /// 0 : Key - string
    /// </summary>
    public class ObjectNode : BaseNode
    {
        /// <summary>Creates the object node.</summary>
        public ObjectNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        /// <summary>Ensures default data, creates input/output ports, and builds the object fields.</summary>
        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();

            SetOptions();
        }

        /// <summary>Adds an "Add Object" entry to the node's context menu.</summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            DropdownMenu menu = evt.menu;

            menu.AppendAction("Add Object", _ => CreateEvent());
            menu.AppendSeparator();
        }

        /// <summary>Seeds default option and port data when none exist.</summary>
        private void SetDefault()
        {
            if (portDatas.Count == 0)
            {
                DataWrapper optionData = new(new GenericData(GenericData.DataType.Object));

                optionDatas.Add(optionData);

                portDatas.Add(new());
            }
        }

        /// <summary>Builds an object field per option entry from its stored object reference.</summary>
        private void SetOptions()
        {
            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> optionData = optionDatas[i].data;

                optionData[0].TryGetObject(out Object target);

                AddEventField(target);
            }
        }

        /// <summary>Appends a new empty object field and its option data with undo support.</summary>
        private void CreateEvent()
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Object");

            DataWrapper optionData = new(new GenericData(GenericData.DataType.Object));

            AddEventField(null);

            optionDatas.Add(optionData);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Builds a draggable row with an object field and a remove button.</summary>
        private void AddEventField(Object target)
        {
            VisualElement container = new() { name = "Item Element" };

            container.AddToClassList("dlg-row");

            ObjectField field = new() { value = target };

            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedCallback);

            Button removeButton = new(() => RemoveObjectField(container)) { text = "X" };

            container.Add(RowDrag.CreateHandle(container, 0, SwapObject, () => RefreshPorts()));
            container.Add(field);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        /// <summary>Swaps two option entries and their row elements for reordering.</summary>
        private void SwapObject(int a, int b)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Reorder Object");

            (optionDatas[a], optionDatas[b]) = (optionDatas[b], optionDatas[a]);

            extensionContainer.Insert(a, extensionContainer[b]);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Removes an object row and its option data, keeping at least one entry.</summary>
        private void RemoveObjectField(VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            if (container.childCount > 1)
            {
                int optionIndex = container.IndexOf(itemElement);

                Undo.RecordObject(so, "Remove Object");

                optionDatas.RemoveAt(optionIndex);

                container.Remove(itemElement);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        /// <summary>Writes a changed object reference into the matching option data entry.</summary>
        private void ChangedCallback(ChangeEvent<Object> evt)
        {
            DialogueSO so = window.SO;

            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");

            int optionIndex = itemElement.parent.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Object Option");

            optionDatas[optionIndex].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}