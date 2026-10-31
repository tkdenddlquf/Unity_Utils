using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public abstract class BaseNode : Node
    {
        protected DialogueEditorWindow window;
        private bool extensionBuilt;

        protected const int ITEM_MIN_WIDTH = 150;

        protected readonly List<DataWrapper> portDatas;
        protected readonly List<DataWrapper> optionDatas;

        public string GUID { get; private set; }

        public Vector2 GraphPosition { get; set; }

        protected BaseNode(DialogueEditorWindow window, string guid)
        {
            this.window = window;
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
            AddPortAccent(port);
            inputContainer.Add(port);

            return port;
        }

        protected Port CreateOutputPort(bool accent = true)
        {
            Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            port.portName = "Output";

            if (accent) AddPortAccent(port);

            outputContainer.Add(port);

            return port;
        }

        private void AddPortAccent(Port port)
        {
            VisualElement line = new();
            line.AddToClassList("dlg-port-line");
            line.AddToClassList("dlg-port-line--cap");
            VisualElement connector = port.Q("connector");
            int index = connector != null && port.IndexOf(connector) >= 0 ? port.IndexOf(connector) + 1 : 1;

            port.Insert(index, line);
            RegisterPortJump(line, port);
        }

        protected void RegisterPortJump(VisualElement bar, Port port)
        {
            bar.tooltip = "더블클릭: 연결된 노드로 이동 (여러 개면 누를 때마다 순환)";
            int cycle = 0;
            bar.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount < 2) return;

                string target = NextConnectedGuid(port, ref cycle);

                if (!string.IsNullOrEmpty(target)) window.FocusNode(target);

                evt.StopPropagation();
            });
        }

        private string NextConnectedGuid(Port port, ref int cycle)
        {
            List<LinkData> links = window.Links;

            if (port.direction == Direction.Output)
            {
                int index = port.parent.IndexOf(port);

                for (int i = 0; i < links.Count; i++)
                {
                    if (links[i].nodeGuid == GUID && links[i].outPortIndex == index) return links[i].targetGuid;
                }

                return "";
            }

            List<string> sources = new();

            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].targetGuid == GUID) sources.Add(links[i].nodeGuid);
            }

            if (sources.Count == 0) return "";

            int pick = cycle % sources.Count;
            cycle = (cycle + 1) % sources.Count;

            return sources[pick];
        }

        protected bool RemovePort(Port port)
        {
            DialogueSO so = window.SO;

            if (portDatas.Count > 1)
            {
                int portIndex = port.parent.IndexOf(port);
                List<LinkData> links = window.Links;

                Undo.RecordObject(so, "Remove Port");

                for (int i = links.Count - 1; i >= 0; i--)
                {
                    LinkData link = links[i];

                    if (link.nodeGuid != GUID) continue;

                    if (link.outPortIndex == portIndex) links.RemoveAt(i);
                    else if (link.outPortIndex > portIndex)
                    {
                        link.outPortIndex--;
                        links[i] = link;
                    }
                }

                window.RemoveEdge(port);
                portDatas.RemoveAt(portIndex);
                outputContainer.Remove(port);
                RefreshExpandedState();
                window.RefreshPortColors();
                EditorUtility.SetDirty(so);
                window.SetUnsaved();
                return true;
            }

            return false;
        }

        protected void SwapPortLinks(int a, int b)
        {
            List<LinkData> links = window.Links;

            for (int i = 0; i < links.Count; i++)
            {
                LinkData link = links[i];

                if (link.nodeGuid != GUID) continue;

                if (link.outPortIndex == a) link.outPortIndex = b;
                else if (link.outPortIndex == b) link.outPortIndex = a;
                else continue;
                links[i] = link;
            }
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

                        if (link.nodeGuid == oldGuid)
                        {
                            link.nodeGuid = evt.newValue;
                            changed = true;
                        }

                        if (link.targetGuid == oldGuid)
                        {
                            link.targetGuid = evt.newValue;
                            changed = true;
                        }

                        if (changed) links[i] = link;
                    }

                    GUID = data.guid;
                    window.OnNodeGuidChanged(oldGuid, GUID);
                    EditorUtility.SetDirty(so);
                    window.SetUnsaved();
                }
            }
        }

        private void AddGUIDField()
        {
            TextField field = new("ID") { value = GUID };
            field.AddToClassList("dlg-field");
            field.RegisterValueChangedCallback(ChangedCallback);
            contentContainer[0].Insert(1, field);
        }
    }
}
