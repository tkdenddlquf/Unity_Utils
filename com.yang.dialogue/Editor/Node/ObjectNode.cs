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
        public ObjectNode(DialogueEditorWindow window, string guid) : base(window, guid)
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

            menu.AppendAction("Add Object", _ => CreateEvent());
            menu.AppendSeparator();
        }

        private void SetDefault()
        {
            if (portDatas.Count == 0)
            {
                DataWrapper optionData = new(new GenericData(GenericData.DataType.Object));

                optionDatas.Add(optionData);

                portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> optionData = optionDatas[i].data;

                optionData[0].TryGetObject(out Object target);

                AddEventField(target);
            }
        }

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

        private void AddEventField(Object target)
        {
            VisualElement container = new() { name = "Item Element" };

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            ObjectField field = new() { value = target };

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(ChangedCallback);

            Button upButton = new(() => MoveObjectField(container, -1)) { text = "▲" };
            Button downButton = new(() => MoveObjectField(container, 1)) { text = "▼" };
            Button removeButton = new(() => RemoveObjectField(container)) { text = "X" };

            container.Add(field);
            container.Add(upButton);
            container.Add(downButton);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void MoveObjectField(VisualElement itemElement, int direction)
        {
            VisualElement container = itemElement.parent;

            if (container == null) return;

            int currentIndex = container.IndexOf(itemElement);
            int newIndex = currentIndex + direction;

            if (newIndex < 0 || newIndex >= container.childCount) return;

            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Move Option Index");

            (optionDatas[currentIndex], optionDatas[newIndex]) = (optionDatas[newIndex], optionDatas[currentIndex]);

            container.Insert(newIndex, itemElement);

            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

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

        private void ChangedCallback(ChangeEvent<Object> evt)
        {
            DialogueSO so = window.SO;

            VisualElement itemElement = FindParent<VisualElement>(evt.target as VisualElement, "Item Element");

            int optionIndex = itemElement.parent.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Object Option");

            optionDatas[optionIndex].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}