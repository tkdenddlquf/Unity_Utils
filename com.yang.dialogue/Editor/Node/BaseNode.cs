using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public abstract class BaseNode : Node
    {
        protected DialogueEditorWindow window;

        protected const int ITEM_MIN_WIDTH = 150;

        protected readonly List<DataWrapper> portDatas;
        protected readonly List<DataWrapper> optionDatas;

        private readonly System.Reflection.FieldInfo portDataField;
        private readonly System.Reflection.FieldInfo optionDataField;

        public string GUID { get; private set; }

        protected BaseNode(DialogueEditorWindow window, string guid)
        {
            this.window = window;

            System.Reflection.FieldInfo expandedButton = typeof(Node).GetField("m_CollapseButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            expandedButton?.SetValue(this, null);

            NodeData data = window.GetNode(guid);

            portDataField = typeof(NodeData).GetField("portDatas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            optionDataField = typeof(NodeData).GetField("optionDatas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            portDatas = (List<DataWrapper>)portDataField.GetValue(data);
            optionDatas = (List<DataWrapper>)optionDataField.GetValue(data);

            GUID = guid;

            AddGUIDField();
        }

        protected override void ToggleCollapse()
        {
            DialogueSO so = window.SO;

            NodeData data = window.GetNode(GUID);

            Undo.RecordObject(so, "Node Expended Change");

            data.expended = !data.expended;

            SetExpendedWithoutNotify(data.expended);

            window.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        public abstract void SetPorts();

        public void SetExpendedWithoutNotify(bool expended)
        {
            if (expended)
            {
                extensionContainer.style.visibility = Visibility.Visible;
                extensionContainer.RemoveFromClassList("hidden");
            }
            else
            {
                extensionContainer.style.visibility = Visibility.Hidden;
                extensionContainer.AddToClassList("hidden");
            }
        }

        protected Port CreateInputPort()
        {
            Port port = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));

            port.portName = "Input";

            inputContainer.Add(port);

            return port;
        }

        protected Port CreateOutputPort()
        {
            Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));

            port.portName = "Output";

            outputContainer.Add(port);

            return port;
        }

        protected void RemovePort(Port port)
        {
            DialogueSO so = window.SO;

            if (portDatas.Count > 1)
            {
                int portIndex = port.parent.IndexOf(port);

                List<LinkData> links = window.Links;

                Undo.RecordObject(so, "Remove Port");

                for (int i = 0; i < links.Count; i++)
                {
                    LinkData link = links[i];

                    if (link.nodeGuid == GUID && link.outPortIndex == portIndex) window.Links.Remove(link);
                }

                window.RemoveEdge(port);

                portDatas.RemoveAt(portIndex);

                outputContainer.Remove(port);

                RefreshExpandedState();

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<string> evt)
        {
            if (evt.target is TextField field)
            {
                DialogueSO so = window.SO;

                if (evt.newValue == so.StartGuid) field.SetValueWithoutNotify(GUID);
                else
                {
                    List<NodeData> nodes = window.Nodes;

                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (nodes[i].guid == evt.newValue)
                        {
                            field.SetValueWithoutNotify(GUID);

                            return;
                        }
                    }

                    NodeData data = window.GetNode(GUID);

                    Undo.RecordObject(so, "Change GUID");

                    data.guid = evt.newValue;

                    window.SetNode(GUID, data);

                    GUID = data.guid;

                    EditorUtility.SetDirty(so);

                    window.SetUnsaved();
                }
            }
        }

        private void AddGUIDField()
        {
            TextField field = new("ID") { value = GUID };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(ChangedCallback);

            contentContainer[0].Insert(1, field);
        }
    }
}