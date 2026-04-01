using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization;
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

        private string saveData;

        public IReadOnlyList<LocalizationTableCollection> collections;

        public List<string> Tables { get; } = new();

        private DialogueSO so;
        public DialogueSO SO
        {
            get => so;
            set
            {
                SetTables();
                CheckSave();

                so = value;

                saveData = JsonUtility.ToJson(value);

                if (value != null)
                {
                    Nodes = (List<NodeData>)nodeField.GetValue(value);
                    Links = (List<LinkData>)linkField.GetValue(value);

                    graph.UpdateViewTransform(value.position, value.scale);
                }
                else
                {
                    Nodes = null;
                    Links = null;
                }

                ResetView();
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

            EditorApplication.quitting -= CheckSave;
            EditorApplication.quitting += CheckSave;

            graph.graphViewChanged -= OnGraphViewChanged;
            graph.graphViewChanged += OnGraphViewChanged;

            graph.viewTransformChanged -= OnViewTransformChanged;
            graph.viewTransformChanged += OnViewTransformChanged;

            graph.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);

            SO = SO;

            ResetView();
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(graph);

            Undo.undoRedoPerformed -= OnUndoRedo;

            Undo.postprocessModifications -= OnPostprocessModifications;

            EditorApplication.quitting -= CheckSave;

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

        private void CheckSave()
        {
            if (!hasUnsavedChanges) return;

            bool save = EditorUtility.DisplayDialog(
                "Unsaved Changes",
                "There are unsaved changes. Do you want to save them before quitting?",
                "Save",
                "Don't Save"
            );

            if (save) SaveChanges();
            else DiscardChanges();
        }

        public override void SaveChanges()
        {
            if (SO == null) return;

            AssetDatabase.SaveAssetIfDirty(SO);

            saveData = JsonUtility.ToJson(SO);

            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            if (SO == null) return;

            JsonUtility.FromJsonOverwrite(saveData, SO);

            AssetDatabase.SaveAssetIfDirty(SO);

            base.DiscardChanges();
        }

        public void OnUndoRedo() => ResetView();

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
                    ResetView();

                    break;
                }
            }

            return mods;
        }

        private void SetTables()
        {
            collections = LocalizationEditorSettings.GetStringTableCollections();

            Tables.Clear();

            if (collections != null)
            {
                foreach (LocalizationTableCollection collection in collections)
                {
                    string tableName = collection.TableCollectionName;
                    string group = collection.Group;

                    Tables.Add(string.IsNullOrEmpty(group) ? tableName : $"{group}/{tableName}");
                }
            }
        }

        private void OnViewTransformChanged(GraphView graphView)
        {
            if (SO == null) return;

            Vector3 position = graph.contentViewContainer.resolvedStyle.translate;
            Vector3 scale = graph.contentViewContainer.resolvedStyle.scale.value;

            if (SO.position == position && SO.scale == scale) return;

            SO.position = position;
            SO.scale = scale;

            hasUnsavedChanges = true;
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

        private void CreateNode(NodeType type, string guid, Vector2 position)
        {
            BaseNode node = graph.CreateNode(type, guid, position);

            node.SetPosition(new Rect(position, Vector2.zero));

            graph.AddElement(node);

            if (guid == SO.StartGuid) node.capabilities &= ~Capabilities.Deletable;
        }

        private void ResetView()
        {
            if (SO != null)
            {
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

                for (int i = Links.Count - 1; i >= 0; i--)
                {
                    LinkData link = Links[i];

                    BaseNode fromNode = null;
                    BaseNode toNode = null;

                    NodeData fromData = default;
                    NodeData toData = default;

                    foreach (Node node in graph.nodes)
                    {
                        if (node is BaseNode baseNode)
                        {
                            NodeData data = GetNode(baseNode.GUID);

                            if (data.guid == link.nodeGuid)
                            {
                                fromNode = baseNode;
                                fromData = data;
                            }
                            else if (data.guid == link.targetGuid)
                            {
                                toNode = baseNode;
                                toData = data;
                            }

                            if (fromNode != null && toNode != null)
                            {
                                if (fromData.PortDatas.Count > link.outPortIndex)
                                {
                                    Port fromPort = fromNode.outputContainer[link.outPortIndex] as Port;
                                    Port toPort = toNode.inputContainer[0] as Port;

                                    Edge edge = fromPort.ConnectTo(toPort);

                                    graph.AddElement(edge);
                                }
                                else Links.Remove(link);
                            }
                        }
                    }
                }
            }

            graph.ClearSelection();
            graph.MarkDirtyRepaint();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.edgesToCreate != null) CreateEdge(change.edgesToCreate);

            if (change.elementsToRemove != null) RemoveNode(change.elementsToRemove);

            if (change.movedElements != null) MoveNode(change.movedElements);

            EditorUtility.SetDirty(SO);

            SetUnsaved();

            return change;
        }
        #endregion

        #region Data
        public NodeData GetNode(string guid)
        {
            if (guid == SO.StartGuid) return (NodeData)startField.GetValue(SO);

            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].guid == guid) return Nodes[i];
            }

            return default;
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

        public void CreateEdge(List<Edge> selectable)
        {
            Undo.RecordObject(SO, "Create Edge");

            foreach (Edge edge in selectable)
            {
                LinkData link = CreateLink(edge);

                if (!Links.Contains(link)) Links.Add(link);
            }
        }

        public void RemoveNode<T>(List<T> selectable) where T : ISelectable
        {
            Undo.RecordObject(SO, "Remove Node");

            foreach (T element in selectable)
            {
                if (element is Edge edge)
                {
                    LinkData link = CreateLink(edge);

                    if (Links.Contains(link)) Links.Remove(link);
                }
                else if (element is BaseNode node)
                {
                    if (node.GUID == SO.StartGuid) continue;

                    for (int i = 0; i < Nodes.Count; i++)
                    {
                        if (Nodes[i].guid == node.GUID)
                        {
                            Nodes.RemoveAt(i);

                            break;
                        }
                    }
                }
            }
        }

        private void MoveNode<T>(List<T> selectable) where T : ISelectable
        {
            Undo.RecordObject(SO, "Move Node");

            foreach (T element in selectable)
            {
                if (element is BaseNode node)
                {
                    if (node.GUID == SO.StartGuid)
                    {
                        NodeData data = (NodeData)startField.GetValue(SO);

                        data.position = node.GetPosition().position;

                        startField.SetValue(SO, data);
                    }
                    else
                    {
                        for (int i = 0; i < Nodes.Count; i++)
                        {
                            if (Nodes[i].guid == node.GUID)
                            {
                                NodeData data = Nodes[i];

                                data.position = node.GetPosition().position;

                                Nodes[i] = data;
                                break;
                            }
                        }
                    }
                }
            }
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
        #endregion
    }
}