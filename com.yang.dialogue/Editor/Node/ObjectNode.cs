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

            evt.menu.AppendAction("Add Object", _ => CreateEvent());
        }

        private void SetDefault()
        {
            NodeData data = window.GetNode(GUID);

            if (data.portDatas.Count == 0)
            {
                DataWrapper optionData = new(new GenericData(GenericData.DataType.Object));

                data.optionDatas.Add(optionData);

                data.portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            IReadOnlyList<DataWrapper> optionDatas = data.optionDatas;

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
            NodeData data = window.GetNode(GUID);

            Undo.RecordObject(so, "Create Object");

            DataWrapper optionData = new(new GenericData(GenericData.DataType.Object));

            AddEventField(null);

            data.optionDatas.Add(optionData);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddEventField(Object target)
        {
            DialogueSO so = window.SO;
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            ObjectField field = new() { value = target };

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));

            Button removeButton = new(() => RemoveEventField(container)) { text = "X" };

            container.Add(field);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void RemoveEventField(VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            VisualElement container = itemElement.parent;

            if (container.childCount > 1)
            {
                int optionIndex = container.IndexOf(itemElement);

                Undo.RecordObject(so, "Remove Object");

                data.optionDatas.RemoveAt(optionIndex);

                container.Remove(itemElement);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<Object> evt, VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            VisualElement container = itemElement.parent;

            int optionIndex = container.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Object Option");

            data.optionDatas[optionIndex].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}