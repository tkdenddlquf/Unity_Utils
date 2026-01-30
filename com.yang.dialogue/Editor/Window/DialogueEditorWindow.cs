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

        public DialogueSO SO { get; private set; }

        private void OnEnable()
        {
            graph = new DialogueGraph(this);
            graph.StretchToParentSize();

            rootVisualElement.Add(graph);

            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;

            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;

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

            Selection.selectionChanged -= OnSelectionChanged;

            graph.graphViewChanged -= OnGraphViewChanged;

            graph.UnregisterCallback<KeyDownEvent>(OnKeyDownEvent);
        }

        [MenuItem("Tools/Dialogue")]
        public static void Open() => GetWindow<DialogueEditorWindow>("Dialogue");

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

                    if (!SO.ContainsLink(link))
                    {
                        Undo.RecordObject(SO, "Create Edge");

                        SO.AddLink(link);
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

                        if (SO.ContainsLink(link))
                        {
                            Undo.RecordObject(SO, "Remove Edge");

                            SO.RemoveLink(link);
                        }
                    }
                    else if (element is BaseNode node)
                    {
                        Undo.RecordObject(SO, "Remove Node");

                        SO.RemoveNode(node.GUID);
                    }
                }
            }

            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is BaseNode node)
                    {
                        if (SO.TryGetNode(node.GUID, out NodeData data))
                        {
                            Undo.RecordObject(SO, "Move Node");

                            data.position = node.GetPosition().position;

                            SO.SetNode(node.GUID, data);
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
                NodeData outputData = SO.GetNode(outputView.GUID);
                NodeData inputData = SO.GetNode(inputView.GUID);

                LinkData link = new()
                {
                    nodeGuid = outputData.guid,
                    portName = outputPort.portName,

                    targetGuid = inputData.guid,
                    targetPortName = inputPort.portName,
                };

                return link;
            }

            return default;
        }

        private void OnSelectionChanged()
        {
            if (graph == null) return;

            if (Selection.activeObject is DialogueSO so)
            {
                SO = so;

                if (so != null) RefreshView();
            }
        }

        private void RefreshView()
        {
            foreach (Node node in graph.nodes) graph.RemoveElement(node);
            foreach (Edge edge in graph.edges) graph.RemoveElement(edge);

            NodeData startNode = SO.startNode;

            if (string.IsNullOrEmpty(startNode.guid))
            {
                SO.startNode = new NodeData(DialogueType.Node.Start);

                startNode = SO.startNode;
            }

            CreateNode(DialogueType.Node.Start, startNode.guid, startNode.position);

            IReadOnlyList<NodeData> nodes = SO.GetNodes();

            for (int i = 0; i < nodes.Count; i++)
            {
                NodeData node = nodes[i];

                CreateNode(node.type, node.guid, node.position);
            }

            IReadOnlyList<LinkData> links = SO.GetLinks();

            for (int i = 0; i < links.Count; i++)
            {
                LinkData link = links[i];

                BaseNode outputNode = null;
                BaseNode inputNode = null;

                foreach (Node node in graph.nodes)
                {
                    if (node is BaseNode view)
                    {
                        NodeData data = SO.GetNode(view.GUID);

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
                    SO.RemoveLink(link);

                    i--;

                    continue;
                }

                Port outputPort = outputNode.outputContainer.Query<Port>().Where(x => x.portName == link.portName).First();
                Port inputPort = inputNode.inputContainer.Query<Port>().Where(x => x.portName == link.targetPortName).First();

                if (outputPort == null || inputPort == null)
                {
                    SO.RemoveLink(link);

                    i--;

                    continue;
                }

                Edge edge = outputPort.ConnectTo(inputPort);

                graph.AddElement(edge);
            }

            graph.ClearSelection();
            graph.MarkDirtyRepaint();
        }

        private void CreateNode(DialogueType.Node type, string guid, Vector2 position)
        {
            BaseNode node = graph.CreateNode(type, guid, position);

            node.SetPosition(new Rect(position, Vector2.zero));

            graph.AddElement(node);

            if (guid == SO.startNode.guid) node.capabilities &= ~Capabilities.Deletable;
        }
        #endregion
    }
}