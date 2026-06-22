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

        public string GUID { get; private set; }

        protected BaseNode(DialogueEditorWindow window, string guid)
        {
            this.window = window;

            System.Reflection.FieldInfo expandedButton = typeof(Node).GetField("m_CollapseButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            expandedButton?.SetValue(this, null);

            NodeData data = window.GetNode(guid);

            portDatas = data.EditorPortDatas;
            optionDatas = data.EditorOptionDatas;

            GUID = guid;

            AddGUIDField();
        }

        protected override void ToggleCollapse()
        {
            DialogueSO so = window.SO;

            NodeData data = window.GetNode(GUID);

            Undo.RecordObject(so, "Node Expended Change");

            data.expended = !data.expended;

            if (data.expended) EnsureExtension();

            SetExpendedWithoutNotify(data.expended);

            window.SetNode(GUID, data);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        public abstract void SetPorts();

        private bool extensionBuilt;

        /// <summary>
        /// Heavy, hidden-while-collapsed UI. Override in nodes that build it lazily; it runs once,
        /// either at create time (when expanded) or the first time the node is expanded.
        /// </summary>
        protected virtual void BuildExtension() { }

        public void EnsureExtension()
        {
            if (extensionBuilt) return;

            extensionBuilt = true;

            BuildExtension();

            RefreshExpandedState();
        }

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

        protected bool RemovePort(Port port)
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

                return true;
            }

            return false;
        }

        private void ChangedCallback(ChangeEvent<string> evt)
        {
            if (evt.target is TextField field)
            {
                DialogueSO so = window.SO;

                if (string.IsNullOrWhiteSpace(evt.newValue) || evt.newValue == so.StartGuid)
                {
                    field.SetValueWithoutNotify(GUID);
                }
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

                    string oldGuid = GUID;

                    NodeData data = window.GetNode(oldGuid);

                    Undo.RecordObject(so, "Change GUID");

                    data.guid = evt.newValue;

                    window.SetNode(oldGuid, data);

                    List<LinkData> links = window.Links;

                    for (int i = 0; i < links.Count; i++)
                    {
                        LinkData link = links[i];

                        bool changed = false;

                        if (link.nodeGuid == oldGuid) { link.nodeGuid = evt.newValue; changed = true; }
                        if (link.targetGuid == oldGuid) { link.targetGuid = evt.newValue; changed = true; }

                        if (changed) links[i] = link;
                    }

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