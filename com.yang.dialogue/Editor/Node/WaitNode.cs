using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class WaitNode : BaseNode
    {
        public WaitNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        public override void SetPorts()
        {
            SetDefault();

            CreatePort(Direction.Input, Port.Capacity.Multi);
            CreatePort(Direction.Output, Port.Capacity.Single);

            SetOptions();
        }

        private void SetDefault()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            if (data.OptionCount == 0)
            {
                string id = CreateID(DialogueType.WAIT_TYPE_000);
                OptionData optionData = new(DialogueType.WAIT_TYPE_000);

                optionData.datas.Add(id);
                optionData.datas.Add(EMPTY_OPTION);
                optionData.datas.Add(EMPTY_OPTION);

                data.AddOption(optionData);

                so.SetNode(GUID, data);
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            OptionData option = data.GetOption(0);

            List<string> datas = option.datas;

            Enum.TryParse(datas[1], out WaitType type);

            EnumField dropdown = new("Type", type);

            dropdown.labelElement.style.minWidth = StyleKeyword.Auto;
            dropdown.labelElement.style.width = StyleKeyword.Auto;

            VisualElement box = dropdown[1];

            box.style.minWidth = ITEM_MIN_WIDTH;

            dropdown.RegisterValueChangedCallback(evt => ChangedCallback(evt, 0, 1));

            FloatField seconds = new("Seconds");

            float.TryParse(datas[2], out float value);

            seconds.value = value;

            seconds.labelElement.style.minWidth = StyleKeyword.Auto;
            seconds.labelElement.style.width = StyleKeyword.Auto;

            seconds.style.minWidth = ITEM_MIN_WIDTH;
            seconds.RegisterValueChangedCallback(evt => ChangedCallback(evt, 0, 2));

            SetDisplaySeconds(seconds, type);

            extensionContainer.Add(dropdown);
            extensionContainer.Add(seconds);
        }

        private void SetDisplaySeconds(VisualElement seconds, WaitType type)
        {
            switch (type)
            {
                case WaitType.Notify:
                    seconds.style.display = DisplayStyle.None;
                    break;

                case WaitType.Seconds:
                    seconds.style.display = DisplayStyle.Flex;
                    break;
            }
        }

        private void ChangedCallback(ChangeEvent<Enum> evt, int optionIndex, int dataIndex)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            Undo.RecordObject(so, "Change Wait Option");

            OptionData optionData = data.GetOption(optionIndex);

            optionData.datas[dataIndex] = evt.newValue.ToString();

            VisualElement seconds = extensionContainer[extensionContainer.childCount - 1];

            SetDisplaySeconds(seconds, (WaitType)evt.newValue);

            data.SetOption(optionIndex, optionData);
            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<float> evt, int optionIndex, int dataIndex)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            Undo.RecordObject(so, "Change Wait Second");

            OptionData optionData = data.GetOption(optionIndex);

            optionData.datas[dataIndex] = evt.newValue.ToString();

            data.SetOption(optionIndex, optionData);
            so.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }
    }
}