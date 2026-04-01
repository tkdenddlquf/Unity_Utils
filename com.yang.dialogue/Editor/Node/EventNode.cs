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

        public EventNode(DialogueEditorWindow window, string guid) : base(window, guid)
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

            menu.AppendAction("Add Event", _ => CreateEvent());
            menu.AppendSeparator();
        }

        private void SetDefault()
        {
            if (portDatas.Count == 0)
            {
                DataWrapper optionData = new(new GenericData(GenericData.DataType.String));

                optionDatas.Add(optionData);

                portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            KeyConverter.GetKeys(window.SO.Events, events);

            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> optionData = optionDatas[i].data;

                string key = optionData[0].ToString();

                AddEventField(key);
            }
        }

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

        private void AddEventField(string key)
        {
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            KeyConverter.GetKeys(window.SO.Events, events);

            int index = events.IndexOf(key);

            PopupField<string> field = new(events, index);

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Delete)
                {
                    field.value = "";
                    window.SetUnsaved();
                }
            });

            Button removeButton = new(() => RemoveEventField(container)) { text = "X" };

            container.Add(field);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

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

        private void ChangedCallback(ChangeEvent<string> evt, VisualElement itemElement)
        {
            DialogueSO so = window.SO;

            VisualElement container = itemElement.parent;

            int optionIndex = container.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Event Option");

            optionDatas[optionIndex].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}