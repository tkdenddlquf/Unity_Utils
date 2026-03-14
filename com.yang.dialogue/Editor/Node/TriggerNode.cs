using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (Unused)
    /// 
    /// Option Data (Common)
    /// 0 : Key - string
    /// 1 : Check - bool
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

            evt.menu.AppendAction("Add Trigger", _ => CreateTrigger());
        }

        private void SetDefault()
        {
            NodeData data = window.GetNode(GUID);

            if (data.portDatas.Count == 0)
            {
                DataWrapper optionData = new(
                    new(GenericData.DataType.String),
                    new(true)
                );

                data.optionDatas.Add(optionData);

                data.portDatas.Add(new());
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            KeyConverter.GetKeys(so.Conditions, conditions);

            IReadOnlyList<DataWrapper> optionDatas = data.optionDatas;

            for (int i = 0; i < optionDatas.Count; i++)
            {
                IReadOnlyList<GenericData> optionData = optionDatas[i].data;

                string key = optionData[0].ToString();

                if (optionData[1].TryGetBool(out bool result)) AddTriggerField(key, result);
            }
        }

        private void CreateTrigger()
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            Undo.RecordObject(so, "Create Trigger");

            DataWrapper optionData = new(
                new(GenericData.DataType.String),
                new(true)
            );

            AddTriggerField("", true);

            data.optionDatas.Add(optionData);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddTriggerField(string key, bool check)
        {
            DialogueSO so = window.SO;
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            Toggle toggle = new() { value = check };

            toggle.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));

            KeyConverter.GetKeys(so.Conditions, conditions);

            int index = conditions.IndexOf(key);

            PopupField<string> field = new(conditions, index);

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, container));

            Button removeButton = new(() => RemoveTriggerField(container)) { text = "X" };

            container.Add(toggle);
            container.Add(field);
            container.Add(removeButton);

            extensionContainer.Add(container);
        }

        private void RemoveTriggerField(VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            VisualElement container = itemElement.parent;

            if (container.childCount > 1)
            {
                int optionIndex = container.IndexOf(itemElement);

                Undo.RecordObject(so, "Remove Trigger");

                data.optionDatas.RemoveAt(optionIndex);

                container.Remove(itemElement);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<string> evt, VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            VisualElement container = itemElement.parent;

            int optionIndex = container.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Trigger Option");

            data.optionDatas[optionIndex].data[0] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<bool> evt, VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            VisualElement container = itemElement.parent;

            int optionIndex = container.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Trigger Check Option");

            data.optionDatas[optionIndex].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}