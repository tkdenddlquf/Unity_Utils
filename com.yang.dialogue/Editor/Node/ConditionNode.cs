using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Port Data (0 : Default)
    /// 0 : PortType - enum
    /// 
    /// Port Data (Common)
    /// 0 : PortType - enum
    /// 1~ : Condition - string
    /// 
    /// Option Data (Unused)
    /// </summary>
    public class ConditionNode : BaseNode
    {
        private enum PortType
        {
            Default,
            Single,
            Multi,
        }

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

            evt.menu.AppendAction("Add Single Port", _ => CreatePort(PortType.Single));
            evt.menu.AppendAction("Add Multi Port", _ => CreatePort(PortType.Multi));
        }

        private void SetDefault()
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            if (data.portDatas.Count == 0)
            {
                DataWrapper portData = new(new GenericData(PortType.Default));

                data.portDatas.Add(portData);
            }
        }

        private void SetOptions()
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            KeyConverter.GetKeys(so.Conditions, conditions);

            IReadOnlyList<DataWrapper> portDatas = data.portDatas;

            for (int i = 0; i < portDatas.Count; i++)
            {
                IReadOnlyList<GenericData> portOptions = portDatas[i].data;

                if (portOptions[0].TryGetEnum(out PortType portType))
                {
                    switch (portType)
                    {
                        case PortType.Default:
                            AddDefaultPort();
                            break;

                        case PortType.Single:
                            {
                                int index = conditions.IndexOf(portOptions[1].ToString());

                                AddSinglePort(index);
                            }
                            break;

                        case PortType.Multi:
                            {
                                VisualElement itemContainer = AddMultiPort();

                                for (int j = 1; j < portOptions.Count; j++)
                                {
                                    int index = conditions.IndexOf(portOptions[j].ToString());

                                    AddConditionField(outputContainer[i] as Port, itemContainer, index);
                                }
                            }
                            break;
                    }
                }
            }
        }

        private void CreatePort(PortType type)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            Undo.RecordObject(so, "Create Output {type}");

            DataWrapper portOption = new(
                new(type),
                new(GenericData.DataType.String)
            );

            switch (type)
            {
                case PortType.Single:
                    AddSinglePort(-1);
                    break;

                case PortType.Multi:
                    VisualElement itemContainer = AddMultiPort();

                    AddConditionField(outputContainer[data.portDatas.Count] as Port, itemContainer, -1);
                    break;
            }

            data.portDatas.Add(portOption);

            RefreshExpandedState();
            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void MovePort(Port port, int direction)
        {
            VisualElement container = port.parent;

            if (container == null) return;

            int currentIndex = container.IndexOf(port);
            int newIndex = currentIndex + direction;

            if (newIndex < 1 || newIndex >= container.childCount) return;

            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            Undo.RecordObject(so, "Move Port Index");

            List<DataWrapper> portDatas = data.portDatas;

            (portDatas[currentIndex], portDatas[newIndex]) = (portDatas[newIndex], portDatas[currentIndex]);

            container.Insert(newIndex, port);

            RefreshPorts();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<string> evt, Port port)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            int portIndex = port.parent.IndexOf(port);

            Undo.RecordObject(so, "Change Port Option");

            data.portDatas[portIndex].data[1] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void ChangedCallback(ChangeEvent<string> evt, Port port, VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            int portIndex = port.parent.IndexOf(port);
            int itemIndex = itemElement.parent.IndexOf(itemElement);

            Undo.RecordObject(so, "Change Port Option");

            data.portDatas[portIndex].data[itemIndex] = new(evt.newValue);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        #region Default Port
        private void AddDefaultPort()
        {
            Port port = CreateOutputPort();

            VisualElement container = new();

            container.style.flexGrow = 1;
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            Label label = new("Default");

            label.style.flexGrow = 1;
            label.style.minWidth = ITEM_MIN_WIDTH;
            label.style.alignSelf = Align.Center;

            container.Add(label);

            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(container);
        }
        #endregion

        #region Single Port
        private void AddSinglePort(int selectIndex)
        {
            Port port = CreateOutputPort();

            DialogueSO so = window.SO;
            VisualElement container = new();

            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            KeyConverter.GetKeys(so.Conditions, conditions);

            PopupField<string> field = new(conditions, selectIndex);

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, port));

            Button upButton = new(() => MovePort(port, -1)) { text = "▲" };
            Button downButton = new(() => MovePort(port, 1)) { text = "▼" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };

            container.Add(field);
            container.Add(upButton);
            container.Add(downButton);
            container.Add(removeButton);

            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(container);
        }
        #endregion

        #region Multi Port
        private VisualElement AddMultiPort()
        {
            Port port = CreateOutputPort();

            DialogueSO so = window.SO;
            VisualElement portElement = new();

            VisualElement groupContainer = new();

            VisualElement itemContainer = new();
            VisualElement buttonContainer = new();

            VisualElement line = new();

            portElement.style.flexGrow = 1;
            portElement.style.flexDirection = FlexDirection.Row;
            portElement.style.alignItems = Align.Stretch;

            groupContainer.style.flexGrow = 1;
            groupContainer.style.flexDirection = FlexDirection.Column;
            groupContainer.style.alignItems = Align.Stretch;

            line.style.marginTop = 7;
            line.style.marginBottom = 7;
            line.style.width = 2;
            line.style.backgroundColor = UnityEngine.Color.gray;

            itemContainer.style.flexDirection = FlexDirection.Column;
            itemContainer.style.alignItems = Align.Stretch;

            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.alignItems = Align.FlexEnd;
            buttonContainer.style.alignSelf = Align.FlexEnd;

            Button createButton = new(() => CreateCondition(port, itemContainer)) { text = "+" };
            Button upButton = new(() => MovePort(port, -1)) { text = "▲" };
            Button downButton = new(() => MovePort(port, 1)) { text = "▼" };
            Button removeButton = new(() => RemovePort(port)) { text = "X" };

            buttonContainer.Add(createButton);
            buttonContainer.Add(upButton);
            buttonContainer.Add(downButton);
            buttonContainer.Add(removeButton);

            groupContainer.Add(itemContainer);
            groupContainer.Add(buttonContainer);

            portElement.Add(groupContainer);
            portElement.Add(line);

            port.style.height = StyleKeyword.Auto;
            port.Q<Label>("type").style.display = DisplayStyle.None;
            port.Add(portElement);

            return itemContainer;
        }

        private void CreateCondition(Port port, VisualElement itemContainer)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            int portIndex = port.parent.IndexOf(port);

            List<GenericData> portOptionDatas = data.portDatas[portIndex].data;

            Undo.RecordObject(so, "Add Multi Port Condition");

            AddConditionField(port, itemContainer, -1);

            portOptionDatas.Add(new(GenericData.DataType.String));

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private void AddConditionField(Port port, VisualElement itemContainer, int selectIndex)
        {
            DialogueSO so = window.SO;

            VisualElement itemElement = new();

            itemElement.style.flexDirection = FlexDirection.Row;
            itemElement.style.alignItems = Align.Center;

            KeyConverter.GetKeys(so.Conditions, conditions);

            PopupField<string> field = new(conditions, selectIndex);

            field.style.minWidth = ITEM_MIN_WIDTH;
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(evt => ChangedCallback(evt, port, itemElement));

            Button remove = new(() => RemoveConditionField(port, itemElement)) { text = "-" };

            itemElement.Add(field);
            itemElement.Add(remove);

            itemContainer.Add(itemElement);
        }

        private void RemoveConditionField(Port port, VisualElement itemElement)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            VisualElement itemContainer = itemElement.parent;

            int portIndex = port.parent.IndexOf(port);
            int itemIndex = itemContainer.IndexOf(itemElement);

            if (itemIndex > 0)
            {
                Undo.RecordObject(so, "Remove Multi Port Condition");

                itemContainer.Remove(itemElement);

                data.portDatas[portIndex].data.RemoveAt(itemIndex);

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }
        #endregion
    }
}