using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class DialogueEditorWindow : EditorWindow
    {
        private DialogueGraph graph;

        private System.Reflection.FieldInfo startField;

        private System.Reflection.FieldInfo nodeField;
        private System.Reflection.FieldInfo linkField;

        private DialogueSO so;
        public DialogueSO SO
        {
            get => so;
            set
            {
                so = value;

                Nodes = (List<NodeData>)nodeField.GetValue(value);
                Links = (List<LinkData>)linkField.GetValue(value);

                RefreshView();
            }
        }

        public List<NodeData> Nodes { get; private set; }
        public List<LinkData> Links { get; private set; }

        private void OnEnable()
        {
            startField = typeof(DialogueSO).GetField("startNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            nodeField = typeof(DialogueSO).GetField("nodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            linkField = typeof(DialogueSO).GetField("links", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            graph = new DialogueGraph(this);
            graph.StretchToParentSize();

            rootVisualElement.Add(graph);

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;

            graph.graphViewChanged -= OnGraphViewChanged;
            graph.graphViewChanged += OnGraphViewChanged;

            graph.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);

            hasUnsavedChanges = false;

            if (SO != null) RefreshView();
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(graph);

            Undo.undoRedoPerformed -= OnUndoRedo;

            Undo.postprocessModifications -= OnPostprocessModifications;

            graph.graphViewChanged -= OnGraphViewChanged;

            graph.UnregisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        [MenuItem("Tools/Dialogue")]
        public static DialogueEditorWindow Open() => GetWindow<DialogueEditorWindow>("Dialogue");

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.ctrlKey && evt.keyCode == KeyCode.S)
            {
                SaveChanges();

                evt.StopPropagation();
            }
        }

        public override void SaveChanges()
        {
            if (SO == null) return;

            AssetDatabase.SaveAssetIfDirty(SO);

            base.SaveChanges();
        }

        public void OnUndoRedo() => RefreshView();

        public void SetUnsaved() => hasUnsavedChanges = true;

        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] mods)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                UndoPropertyModification mod = mods[i];
                PropertyModification current = mod.currentValue;

                string path = current.propertyPath;

                if (current.target == SO && (path.StartsWith("conditions") || path.StartsWith("events")))
                {
                    RefreshView();

                    break;
                }
            }

            return mods;
        }

        #region View
        public void RemoveEdge(Port port)
        {
            IEnumerator<Edge> enumerator = port.connections.GetEnumerator();

            while (enumerator.MoveNext())
            {
                Edge edge = enumerator.Current;

                if (edge != null)
                {
                    edge.input?.Disconnect(edge);
                    edge.output?.Disconnect(edge);

                    graph.RemoveElement(edge);

                    enumerator.Dispose();
                    enumerator = port.connections.GetEnumerator();
                }
            }
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.edgesToCreate != null)
            {
                foreach (Edge edge in change.edgesToCreate)
                {
                    LinkData link = CreateLink(edge);

                    if (!Links.Contains(link))
                    {
                        Undo.RecordObject(SO, "Create Edge");

                        Links.Add(link);
                    }
                }
            }

            if (change.elementsToRemove != null)
            {
                foreach (GraphElement element in change.elementsToRemove)
                {
                    if (element is Edge edge)
                    {
                        LinkData link = CreateLink(edge);

                        if (Links.Contains(link))
                        {
                            Undo.RecordObject(SO, "Remove Edge");

                            Links.Remove(link);
                        }
                    }
                    else if (element is BaseNode node)
                    {
                        Undo.RecordObject(SO, "Remove Node");

                        RemoveNode(node.GUID);
                    }
                }
            }

            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is BaseNode node)
                    {
                        if (TryGetNode(node.GUID, out NodeData data))
                        {
                            Undo.RecordObject(SO, "Move Node");

                            data.position = node.GetPosition().position;

                            SetNode(node.GUID, data);
                        }
                    }
                }
            }

            EditorUtility.SetDirty(SO);

            SetUnsaved();

            return change;
        }

        private LinkData CreateLink(Edge edge)
        {
            Port outputPort = edge.output;
            Port inputPort = edge.input;

            if (outputPort.node is BaseNode outputView && inputPort.node is BaseNode inputView)
            {
                NodeData outputData = GetNode(outputView.GUID);
                NodeData inputData = GetNode(inputView.GUID);

                LinkData link = new()
                {
                    nodeGuid = outputData.guid,
                    targetGuid = inputData.guid,

                    outPortIndex = outputPort.parent.IndexOf(outputPort),
                };

                return link;
            }

            return default;
        }

        private void RefreshView()
        {
            if (SO == null) return;

            foreach (Node node in graph.nodes) graph.RemoveElement(node);
            foreach (Edge edge in graph.edges) graph.RemoveElement(edge);

            NodeData startNode = (NodeData)startField.GetValue(SO);

            if (string.IsNullOrEmpty(startNode.guid))
            {
                startField.SetValue(SO, new NodeData(NodeType.Start));

                startNode = (NodeData)startField.GetValue(SO);

                EditorUtility.SetDirty(SO);
            }

            CreateNode(NodeType.Start, startNode.guid, startNode.position);

            for (int i = 0; i < Nodes.Count; i++)
            {
                NodeData node = Nodes[i];

                CreateNode(node.type, node.guid, node.position);
            }

            for (int i = 0; i < Links.Count; i++)
            {
                LinkData link = Links[i];

                BaseNode outputNode = null;
                BaseNode inputNode = null;

                foreach (Node node in graph.nodes)
                {
                    if (node is BaseNode view)
                    {
                        NodeData data = GetNode(view.GUID);

                        if (data.guid == link.nodeGuid)
                        {
                            outputNode = view;

                            continue;
                        }

                        if (data.guid == link.targetGuid)
                        {
                            inputNode = view;

                            continue;
                        }
                    }
                }

                if (outputNode == null || inputNode == null)
                {
                    Links.Remove(link);

                    i--;

                    continue;
                }


                if (outputNode.outputContainer[link.outPortIndex] is not Port outputPort || inputNode.inputContainer[0] is not Port inputPort)
                {
                    Links.Remove(link);

                    i--;

                    continue;
                }

                Edge edge = outputPort.ConnectTo(inputPort);

                graph.AddElement(edge);
            }

            graph.ClearSelection();
            graph.MarkDirtyRepaint();
        }

        private void CreateNode(NodeType type, string guid, Vector2 position)
        {
            BaseNode node = graph.CreateNode(type, guid, position);

            node.SetPosition(new Rect(position, Vector2.zero));

            graph.AddElement(node);

            if (guid == SO.StartGuid) node.capabilities &= ~Capabilities.Deletable;
        }
        #endregion

        #region Node
        public NodeData GetNode(string guid)
        {
            if (guid == SO.StartGuid) return (NodeData)startField.GetValue(SO);

            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].guid == guid) return Nodes[i];
            }

            return default;
        }

        public bool ContainsNode(string guid)
        {
            if (guid == SO.StartGuid) return true;

            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].guid == guid) return true;
            }

            return false;
        }

        public bool TryGetNode(string guid, out NodeData node)
        {
            if (guid == SO.StartGuid)
            {
                node = (NodeData)startField.GetValue(SO);

                return true;
            }
            else
            {
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].guid == guid)
                    {
                        node = Nodes[i];

                        return true;
                    }
                }
            }

            node = default;

            return false;
        }

        public void SetNode(string guid, NodeData data)
        {
            if (SO.StartGuid == guid) startField.SetValue(SO, data);
            else
            {
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].guid == guid) Nodes[i] = data;
                }
            }
        }

        public bool RemoveNode(string guid)
        {
            if (guid == SO.StartGuid) return false;

            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].guid == guid)
                {
                    Nodes.RemoveAt(i);

                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Link
        public LinkData GetLink(string guid, int outPortIndex)
        {
            for (int i = 0; i < Links.Count; i++)
            {
                if (Links[i].nodeGuid == guid && Links[i].outPortIndex == outPortIndex) return Links[i];
            }

            return default;
        }
        #endregion
    }
}