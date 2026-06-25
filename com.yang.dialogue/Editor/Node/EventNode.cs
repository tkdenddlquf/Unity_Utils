using System.Collections.Generic;
using UnityEditor;
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
    public class EventNode : BaseNode
    {
        private readonly List<string> events = new();

        /// <summary>Creates the event node.</summary>
        public EventNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        /// <summary>Ensures default data, creates input/output ports, and builds the event fields.</summary>
        public override void SetPorts()
        {
            SetDefault();

            CreateInputPort();
            CreateOutputPort();

            SetOptions();
        }

        /// <summary>Adds an "Add Event" entry to the node's context menu.</summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            DropdownMenu menu = evt.menu;

            menu.AppendAction("Add Event", _ => CreateEvent());
            menu.AppendSeparator();
        }

        /// <summary>Seeds default option and port data when none exist.</summary>
        private void SetDefault()
        {
            if (portDatas.Count == 0)
            {
                DataWrapper optionData = new(new GenericData(GenericData.DataType.String));

                optionDatas.Add(optionData);

                portDatas.Add(new());
            }
        }

        /// <summary>Loads available event keys and builds an event field per option entry.</summary>
        private void SetOptions()
        {
            window.GetKeysInto(window.SO.Events, events);

            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> optionData = optionDatas[i].data;

                string key = optionData[0].ToString();

                AddEventField(key);
            }
        }

        /// <summary>Appends a new empty event field and its option data with undo support.</summary>
        private void CreateEvent()
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Event");

            DataWrapper optionData = new(new GenericData(GenericData.DataType.String));

            AddEventField("");

            optionDatas.Add(optionData);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Builds a draggable row with an event key popup and a remove button.</summary>
        private void AddEventField(string key)
        {
            VisualElement container = new() { name = "Item Element" };

            container.AddToClassList("dlg-row");

            window.GetKeysInto(window.SO.Events, events);

            int index = events.IndexOf(key);

            PopupField<string> field = new(events, index);

            field.AddToClassList("dlg-grow");
            field.RegisterValueChangedCallback(ChangedCallback);
            field.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);

            Button removeButton = new(() => RemoveEventField(container)) { text = "X" };

            container.Add(RowDrag.CreateHandle(container, 0, SwapOption));
            container.Add(field);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        /// <summary>Swaps two option entries and their row elements for reordering.</summary>
        private void SwapOption(int a, int b)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Reorder Event");

            (optionDatas[a], optionDatas[b]) = (optionDatas[b], optionDatas[a]);

            extensionContainer.Insert(a, extensionContainer[b]);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Removes an event row and its option data, keeping at least one entry.</summary>
        private void RemoveEventField(VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            if (container.childCount > 1)
            {
                int optionIndex = container.IndexOf(itemElement);

                Undo.RecordObject(so, "Remove Event");

                optionDatas.RemoveAt(optionIndex);

                container.Remove(itemElement);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        /// <summary>On Delete, clears the focused event field's value.</summary>
        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete)
            {
                PopupField<string> field = evt.target.FindParentInCurrent<PopupField<string>>();

                if (field == null) return;

                field.value = "";

                window.SetUnsaved();
            }
        }

        /// <summary>Writes a changed event key into the matching option data entry.</summary>
        private void ChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;

            VisualElement itemElement = evt.target.FindParent<VisualElement>("Item Element");

            int optionIndex = itemElement.parent.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Event Option");

            optionDatas[optionIndex].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}