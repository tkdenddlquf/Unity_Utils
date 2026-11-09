using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class DialogueGraph : GraphView
    {
        private string pendingSourceGuid;
        private int pendingOutputIndex;
        private Vector2 pendingDropPosition;
        private DialogueSO pendingAsset;
        private bool syncScheduled;
        private bool suppressSelectionTracking;
        private string debugNodeGuid;

        private const float NODE_MARGIN = 1500f;
        private const string COPY_FORMAT = "Yang.Dialogue.NodeClipboard";

        private readonly DialogueEditorWindow window;
        private readonly DialogueEdgeConnectorListener edgeListener;
        private readonly DialogueEdgeSearchProvider edgeSearch;
        private static readonly Color PortLinkedColor = new(0.36f, 0.82f, 0.47f);
        private static readonly Color PortEmptyColor = new(0.55f, 0.55f, 0.55f);
        private static readonly Color PortCapColor = new(0.48f, 0.48f, 0.48f);
        private readonly HashSet<string> visibleGuids = new();
        private readonly Dictionary<string, BaseNode> currentNodes = new();
        private readonly HashSet<string> linkedInputGuids = new();
        private readonly HashSet<(string guid, int port)> linkedOutputPorts = new();
        private readonly HashSet<string> selectedGuids = new();

        public DialogueGraph(DialogueEditorWindow window)
        {
            this.window = window;
            edgeListener = new DialogueEdgeConnectorListener(this);
            edgeSearch = ScriptableObject.CreateInstance<DialogueEdgeSearchProvider>();
            edgeSearch.Initialize(this, window);
            LoadStyleSheet();
            Insert(0, new GridBackground());
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            serializeGraphElements = SerializeNodes;
            unserializeAndPaste = UnserializeNode;
            canPasteSerializedData = CheckSerialize;
            viewTransformChanged += _ => RequestSync();
            RegisterCallback<GeometryChangedEvent>(_ => RequestSync());
            RegisterCallback<ValidateCommandEvent>(OnValidateCommand);
            RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);
        }

        public void Dispose() => Object.DestroyImmediate(edgeSearch);

        public Port CreateOutputPort() => new DialogueOutputPort(edgeListener);

        private void LoadStyleSheet()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:StyleSheet DialogueGraph"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!path.EndsWith("DialogueGraph.uss")) continue;

                StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);

                if (sheet != null)
                {
                    styleSheets.Add(sheet);
                    return;
                }
            }
        }

        #region Virtualize
        public void RebuildAll()
        {
            HashSet<string> previousSelection = new(selectedGuids);
            suppressSelectionTracking = true;

            foreach (Node node in nodes.ToList()) RemoveElement(node);

            foreach (Edge edge in edges.ToList()) RemoveElement(edge);

            suppressSelectionTracking = false;
            selectedGuids.Clear();

            if (window.SO == null)
            {
                currentNodes.Clear();
                visibleGuids.Clear();
            }

            if (window.SO != null)
            {
                foreach (string guid in previousSelection)
                {
                    if (!string.IsNullOrEmpty(window.GetNode(guid).guid)) selectedGuids.Add(guid);
                }

                SyncVisibleNodes();
                MapCurrentNodes();
                suppressSelectionTracking = true;

                foreach (string guid in selectedGuids)
                {
                    if (currentNodes.TryGetValue(guid, out BaseNode node)) AddToSelection(node);
                }

                suppressSelectionTracking = false;
            }
        }

        public void SetDebugNode(string guid)
        {
            if (debugNodeGuid == guid) return;

            string previous = debugNodeGuid;
            debugNodeGuid = guid;

            if (!string.IsNullOrEmpty(previous) && currentNodes.TryGetValue(previous, out BaseNode prior)) prior.RemoveFromClassList("dlg-debug-current");

            if (!string.IsNullOrEmpty(guid) && currentNodes.TryGetValue(guid, out BaseNode current)) current.AddToClassList("dlg-debug-current");
        }

        // Coalesce consecutive pan/zoom and geometry events into one visibility scan.
        public void RequestSync()
        {
            if (syncScheduled) return;

            syncScheduled = true;
            schedule.Execute(() =>
            {
                syncScheduled = false;
                SyncVisibleNodes();
            });
        }

        public void SyncVisibleNodes()
        {
            if (window.SO == null) return;

            Rect view = GetVisibleContentRect(NODE_MARGIN);

            if (view.width <= 0) return;

            visibleGuids.Clear();
            CollectVisible(window.SO.EditorStartNode, view);
            List<NodeData> all = window.Nodes;

            for (int i = 0; i < all.Count; i++) CollectVisible(all[i], view);

            MapCurrentNodes();
            bool changed = false;
            suppressSelectionTracking = true;

            foreach (KeyValuePair<string, BaseNode> kv in currentNodes)
            {
                if (!visibleGuids.Contains(kv.Key))
                {
                    if (selectedGuids.Contains(kv.Key)) RemoveFromSelection(kv.Value);

                    RemoveElement(kv.Value);
                    changed = true;
                }
            }

            foreach (string guid in visibleGuids)
            {
                if (!currentNodes.ContainsKey(guid))
                {
                    BaseNode node = CreateNode(window.GetNode(guid));

                    if (selectedGuids.Contains(guid)) AddToSelection(node);

                    changed = true;
                }
            }

            suppressSelectionTracking = false;

            if (changed) RebuildEdges();
        }

        private void CollectVisible(NodeData data, Rect view)
        {
            if (!string.IsNullOrEmpty(data.guid) && view.Contains(data.position)) visibleGuids.Add(data.guid);
        }

        private void MapCurrentNodes()
        {
            currentNodes.Clear();

            foreach (Node node in nodes)
            {
                if (node is BaseNode baseNode) currentNodes[baseNode.GUID] = baseNode;
            }
        }

        private void RebuildEdges()
        {
            foreach (Edge edge in edges.ToList()) RemoveElement(edge);

            MapCurrentNodes();
            List<LinkData> links = window.Links;

            for (int i = 0; i < links.Count; i++)
            {
                LinkData link = links[i];

                if (!currentNodes.TryGetValue(link.nodeGuid, out BaseNode fromNode)) continue;

                if (!currentNodes.TryGetValue(link.targetGuid, out BaseNode toNode)) continue;

                if (link.outPortIndex < 0 || link.outPortIndex >= fromNode.outputContainer.childCount) continue;

                if (toNode.inputContainer.childCount == 0) continue;

                Port fromPort = fromNode.outputContainer[link.outPortIndex] as Port;
                Port toPort = toNode.inputContainer[0] as Port;

                if (fromPort != null && toPort != null) AddElement(fromPort.ConnectTo(toPort));
            }

            RefreshPortColors();
        }

        public void RefreshPortColors()
        {
            if (window.SO == null) return;

            linkedInputGuids.Clear();
            linkedOutputPorts.Clear();
            List<LinkData> links = window.Links;

            for (int i = 0; i < links.Count; i++)
            {
                LinkData link = links[i];
                linkedInputGuids.Add(link.targetGuid);
                linkedOutputPorts.Add((link.nodeGuid, link.outPortIndex));
            }

            foreach (Node node in nodes)
            {
                if (node is not BaseNode baseNode) continue;

                if (baseNode.inputContainer.childCount > 0 && baseNode.inputContainer[0] is Port inPort) ColorPort(inPort, linkedInputGuids.Contains(baseNode.GUID));

                for (int i = 0; i < baseNode.outputContainer.childCount; i++)
                {
                    if (baseNode.outputContainer[i] is Port outPort) ColorPort(outPort, linkedOutputPorts.Contains((baseNode.GUID, i)));
                }
            }
        }

        private static void ColorPort(Port port, bool linked)
        {
            port.portColor = PortCapColor;
            VisualElement line = port.Q<VisualElement>(null, "dlg-port-line");

            if (line == null) return;

            if (linked)
            {
                line.style.backgroundColor = PortLinkedColor;
                SetBarBorder(line, 0f, Color.clear);
            }
            else
            {
                line.style.backgroundColor = Color.clear;
                SetBarBorder(line, 1f, PortEmptyColor);
            }
        }

        private static void SetBarBorder(VisualElement element, float width, Color color)
        {
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }

        public void FocusNode(string guid, bool select = false)
        {
            if (window.SO == null) return;

            NodeData data = window.GetNode(guid);

            if (string.IsNullOrEmpty(data.guid)) return;

            CenterOn(data.position, new Vector2(220f, 120f));
            SyncVisibleNodes();
            BaseNode node = FindVisibleNode(data.guid);

            if (node == null) return;

            if (select)
            {
                ClearSelection();
                AddToSelection(node);
            }

            if (node.layout.width > 0f)
            {
                CenterOn(data.position, node.layout.size);
            }
            else
            {
                void Recenter(GeometryChangedEvent _)
                {
                    node.UnregisterCallback<GeometryChangedEvent>(Recenter);
                    CenterOn(data.position, node.layout.size);
                }
                node.RegisterCallback<GeometryChangedEvent>(Recenter);
            }
        }

        private void CenterOn(Vector2 topLeft, Vector2 size)
        {
            Vector3 scale = style.scale.value.value;
            Vector2 viewCenter = layout.size * 0.5f;
            Vector2 nodeCenter = topLeft + size * 0.5f;

            UpdateViewTransform(new Vector3(viewCenter.x - nodeCenter.x * scale.x, viewCenter.y - nodeCenter.y * scale.y, 0f), scale);
        }

        private BaseNode FindVisibleNode(string guid)
        {
            foreach (Node node in nodes)
            {
                if (node is BaseNode baseNode && baseNode.GUID == guid) return baseNode;
            }

            return null;
        }

        private Rect GetVisibleContentRect(float margin)
        {
            Rect rect = contentRect;

            if (rect.width <= 1 || rect.height <= 1) return Rect.zero;

            IResolvedStyle style = contentViewContainer.resolvedStyle;
            Vector3 translate = style.translate;
            Vector3 scale = style.scale.value;

            if (scale.x == 0 || scale.y == 0) return Rect.zero;

            float minX = (0 - translate.x) / scale.x - margin;
            float maxX = (rect.width - translate.x) / scale.x + margin;
            float minY = (0 - translate.y) / scale.y - margin;
            float maxY = (rect.height - translate.y) / scale.y + margin;

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
        #endregion

        #region Selection
        public void OnNodeGuidChanged(string previous, string current)
        {
            if (selectedGuids.Remove(previous)) selectedGuids.Add(current);

            if (visibleGuids.Remove(previous)) visibleGuids.Add(current);

            if (currentNodes.TryGetValue(previous, out BaseNode node))
            {
                currentNodes.Remove(previous);
                currentNodes[current] = node;
            }

            if (debugNodeGuid == previous) debugNodeGuid = current;

            RefreshPortColors();
        }

        public string FirstSelectedGuid()
        {
            foreach (string guid in selectedGuids) return guid;

            return null;
        }

        public void SelectGuid(string guid)
        {
            ClearSelection();

            if (string.IsNullOrEmpty(guid) || window.SO == null || string.IsNullOrEmpty(window.GetNode(guid).guid)) return;

            selectedGuids.Add(guid);
            MapCurrentNodes();

            if (currentNodes.TryGetValue(guid, out BaseNode node)) AddToSelection(node);
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);

            if (!suppressSelectionTracking && selectable is BaseNode node) selectedGuids.Add(node.GUID);
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);

            if (!suppressSelectionTracking && selectable is BaseNode node) selectedGuids.Remove(node.GUID);
        }

        public override void ClearSelection()
        {
            base.ClearSelection();

            if (!suppressSelectionTracking) selectedGuids.Clear();
        }

        private bool CanLogicalCopy()
        {
            if (selectedGuids.Count == 0) return false;

            foreach (ISelectable selectable in selection)
            {
                if (selectable is BaseNode) return false;
            }

            return true;
        }

        private void OnValidateCommand(ValidateCommandEvent evt)
        {
            if (evt.commandName == "Copy" && CanLogicalCopy()) evt.StopPropagation();
        }

        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            if (evt.commandName == "Copy" && CanLogicalCopy())
            {
                EditorGUIUtility.systemCopyBuffer = BuildCopy();
                evt.StopPropagation();
            }
        }
        #endregion

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            DropdownMenu menu = evt.menu;

            if (window.SO == null)
            {
                menu.AppendAction("Select a Dialogue asset to edit", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            VisualElement target = evt.target as VisualElement;

            while (target != null && target != this && target is not BaseNode) target = target.parent;

            BaseNode clickedNode = target as BaseNode;

            if (Application.isPlaying && clickedNode != null)
                menu.AppendAction("Play / Jump to this node", _ => window.DebugJumpToNode(clickedNode.GUID),

                    window.CanDebugJump ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Copy", MenuActionCopy, selectedGuids.Count == 0 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Paste", MenuActionPaste, CheckSerialize(EditorGUIUtility.systemCopyBuffer) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Remove", MenuActionRemove, selection.Count == 0 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);

            if (clickedNode != null) return;

            Vector2 mousePos = evt.localMousePosition;
            Vector2 nodePos = contentViewContainer.WorldToLocal(mousePos);

            menu.AppendSeparator();
            menu.AppendAction("Add Dialogue", _ => AddNode(NodeType.Dialogue, nodePos));
            menu.AppendAction("Add Choice", _ => AddNode(NodeType.Choice, nodePos));
            menu.AppendSeparator();
            menu.AppendAction("Add Trigger", _ => AddNode(NodeType.Trigger, nodePos));
            menu.AppendAction("Add Event", _ => AddNode(NodeType.Event, nodePos));
            menu.AppendAction("Add Command", _ => AddNode(NodeType.Command, nodePos));
            menu.AppendSeparator();
            menu.AppendAction("Add Condition", _ => AddNode(NodeType.Condition, nodePos));
            menu.AppendSeparator();
            menu.AppendAction("Add Wait", _ => AddNode(NodeType.Wait, nodePos));
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatiblePorts = new();

            foreach (Port port in ports)
            {
                if (startPort.direction == port.direction) continue;

                if (startPort.node == port.node) continue;

                if (startPort.portType != port.portType) continue;

                compatiblePorts.Add(port);
            }

            return compatiblePorts;
        }

        public override EventPropagation DeleteSelection()
        {
            Focusable focused = panel?.focusController?.focusedElement;

            if (focused == this) return base.DeleteSelection();

            return EventPropagation.Stop;
        }

        #region Node
        private void AddNode(NodeType type, Vector2 position)
        {
            if (window.SO == null) return;

            DialogueSO so = window.SO;
            Undo.RecordObject(so, "Create Node");
            NodeData data = new(type) { position = position };
            window.Nodes.Add(data);
            window.InvalidateNodeIndices();
            CreateNode(data);
            RefreshPortColors();
            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        public BaseNode CreateNode(NodeData data)
        {
            BaseNode node = ConvertData(data.type, data.guid);
            node.GraphPosition = data.position;
            node.SetPosition(new(data.position, Vector2.zero));
            node.SetPorts();

            if (data.expended) node.EnsureExtension();

            node.SetExpendedWithoutNotify(data.expended);
            node.RefreshExpandedState();
            node.EnableInClassList("dlg-debug-current", data.guid == debugNodeGuid);
            AddElement(node);

            return node;
        }

        private BaseNode ConvertData(NodeType type, string guid)
        {
            BaseNode node = type switch
            {
                NodeType.Start => new StartNode(window, guid),
                NodeType.Dialogue => new DialogueNode(window, guid),
                NodeType.Condition => new ConditionNode(window, guid),
                NodeType.Trigger => new TriggerNode(window, guid),
                NodeType.Event => new EventNode(window, guid),
                NodeType.Choice => new ChoiceNode(window, guid),
                NodeType.Wait => new WaitNode(window, guid),
                NodeType.Command => new CommandNode(window, guid),
                _ => null,
            };

            if (node != null)
            {
                node.title = type.ToString();
                node.AddToClassList($"node-{type.ToString().ToLowerInvariant()}");
            }

            return node;
        }
        #endregion

        #region Action
        private string SerializeNodes(IEnumerable<GraphElement> elements) => BuildCopy();

        private string BuildCopy()
        {
            if (window.SO == null) return "";

            DialogueSO so = window.SO;
            CopyData data = new() { format = COPY_FORMAT };
            HashSet<string> guids = new();

            foreach (string guid in selectedGuids)
            {
                if (guid == so.StartGuid) continue;

                NodeData nodeData = window.GetNode(guid);

                if (string.IsNullOrEmpty(nodeData.guid)) continue;

                data.nodes.Add(nodeData);
                guids.Add(guid);
            }

            if (data.nodes.Count == 0) return "";

            List<LinkData> links = window.Links;

            for (int i = 0; i < links.Count; i++)
            {
                LinkData link = links[i];

                if (guids.Contains(link.nodeGuid) && guids.Contains(link.targetGuid)) data.links.Add(link);
            }

            return JsonUtility.ToJson(data);
        }

        private void UnserializeNode(string operationName, string data)
        {
            if (!TryReadCopyData(data, out CopyData copyData)) return;

            ClearSelection();
            Vector2 nodeCenterPos = Vector2.zero;

            foreach (NodeData nodeData in copyData.nodes) nodeCenterPos += nodeData.position;

            nodeCenterPos /= copyData.nodes.Count;
            Vector2 newCenterPos = contentViewContainer.WorldToLocal(layout.size * 0.5f);
            Vector2 addedPos = newCenterPos - nodeCenterPos;
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Paste Node");
            Dictionary<string, string> guidMap = new();
            List<NodeData> pastedNodes = new(copyData.nodes.Count);

            foreach (NodeData nodeData in copyData.nodes)
            {
                NodeData newNodeData = new(nodeData);
                newNodeData.position += addedPos;
                guidMap[nodeData.guid] = newNodeData.guid;
                window.Nodes.Add(newNodeData);
                pastedNodes.Add(newNodeData);
            }

            window.InvalidateNodeIndices();

            foreach (NodeData pastedNode in pastedNodes)
            {
                BaseNode node = CreateNode(pastedNode);
                AddToSelection(node);
            }

            if (copyData.links != null)
            {
                for (int i = 0; i < copyData.links.Count; i++)
                {
                    LinkData link = copyData.links[i];

                    if (!guidMap.TryGetValue(link.nodeGuid, out string source)) continue;

                    if (!guidMap.TryGetValue(link.targetGuid, out string target)) continue;

                    window.Links.Add(new LinkData { nodeGuid = source, targetGuid = target, outPortIndex = link.outPortIndex });
                }

                RebuildEdges();
            }

            EditorUtility.SetDirty(so);
            window.SetUnsaved();
        }

        private bool CheckSerialize(string data) => TryReadCopyData(data, out _);

        private bool TryReadCopyData(string data, out CopyData copyData)
        {
            copyData = null;

            if (window.SO == null || string.IsNullOrWhiteSpace(data) || data.IndexOf(COPY_FORMAT, System.StringComparison.Ordinal) < 0) return false;

            try
            {
                CopyData parsed = JsonUtility.FromJson<CopyData>(data);

                if (parsed == null || parsed.format != COPY_FORMAT || parsed.nodes == null || parsed.nodes.Count == 0) return false;

                HashSet<string> guids = new();

                for (int i = 0; i < parsed.nodes.Count; i++)
                {
                    NodeData node = parsed.nodes[i];

                    if (string.IsNullOrEmpty(node.guid) || node.type == NodeType.Start || !System.Enum.IsDefined(typeof(NodeType), node.type) || !guids.Add(node.guid)) return false;

                    if (node.PortDatas == null || node.OptionDatas == null) return false;

                    for (int j = 0; j < node.PortDatas.Count; j++)
                    {
                        if (node.PortDatas[j].data == null) return false;
                    }

                    for (int j = 0; j < node.OptionDatas.Count; j++)
                    {
                        if (node.OptionDatas[j].data == null) return false;
                    }
                }

                if (parsed.links != null)
                {
                    for (int i = 0; i < parsed.links.Count; i++)
                    {
                        LinkData link = parsed.links[i];

                        if (!guids.Contains(link.nodeGuid) || !guids.Contains(link.targetGuid) || link.outPortIndex < 0) return false;
                    }
                }

                copyData = parsed;
                return true;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
        }

        private void MenuActionCopy(DropdownMenuAction menuAction)
        {
            string data = BuildCopy();

            if (!string.IsNullOrEmpty(data)) EditorGUIUtility.systemCopyBuffer = data;
        }

        private void MenuActionPaste(DropdownMenuAction menuAction) => UnserializeNode("Paste", EditorGUIUtility.systemCopyBuffer);

        private void MenuActionRemove(DropdownMenuAction menuAction)
        {
            if (window.SO == null) return;

            List<GraphElement> elements = new();

            foreach (ISelectable selected in selection)
            {
                if (selected is GraphElement element && (element is not BaseNode node || node.GUID != window.SO.StartGuid)) elements.Add(element);
            }

            if (elements.Count > 0) DeleteElements(elements);
        }

        #region Edge Search
        private void OpenEdgeSearch(Port output, Vector2 position)
        {
            if (window.SO == null || output.node is not BaseNode source) return;

            int index = output.parent.IndexOf(output);

            if (index < 0) return;

            pendingSourceGuid = source.GUID;
            pendingOutputIndex = index;
            pendingDropPosition = contentViewContainer.WorldToLocal(position);
            pendingAsset = window.SO;
            SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(position)), edgeSearch);
        }

        public void CompleteEdgeSearch(object result)
        {
            if (window.SO == null || window.SO != pendingAsset) return;

            NodeData source = window.GetNode(pendingSourceGuid);

            if (string.IsNullOrEmpty(source.guid)) return;

            if (result is NodeType type)
            {
                if (type == NodeType.Start) return;

                DialogueSO so = window.SO;
                Undo.RecordObject(so, "Create Connected Node");
                NodeData data = new(type) { position = pendingDropPosition };
                window.Nodes.Add(data);
                window.InvalidateNodeIndices();
                CreateNode(data);
                ConnectOutput(data.guid, false);
            }
            else if (result is string targetGuid)
            {
                ConnectOutput(targetGuid, true);
            }
        }

        private void ConnectOutput(string targetGuid, bool recordUndo)
        {
            if (targetGuid == pendingSourceGuid || targetGuid == window.SO.StartGuid) return;

            NodeData target = window.GetNode(targetGuid);

            if (string.IsNullOrEmpty(target.guid)) return;

            if (recordUndo) Undo.RecordObject(window.SO, "Connect Nodes");

            List<LinkData> links = window.Links;

            for (int i = links.Count - 1; i >= 0; i--)
            {
                if (links[i].nodeGuid == pendingSourceGuid && links[i].outPortIndex == pendingOutputIndex) links.RemoveAt(i);
            }

            links.Add(new LinkData { nodeGuid = pendingSourceGuid, targetGuid = targetGuid, outPortIndex = pendingOutputIndex });
            RebuildEdges();
            EditorUtility.SetDirty(window.SO);
            window.SetUnsaved();
        }

        private sealed class DialogueOutputPort : Port
        {
            public DialogueOutputPort(IEdgeConnectorListener listener) : base(Orientation.Horizontal, Direction.Output, Capacity.Single, typeof(bool))
            {
                m_EdgeConnector = new EdgeConnector<Edge>(listener);
                this.AddManipulator(m_EdgeConnector);
            }
        }

        private sealed class DialogueEdgeConnectorListener : IEdgeConnectorListener
        {
            private readonly DialogueGraph graph;
            private readonly List<Edge> edgesToCreate = new(1);
            private readonly List<GraphElement> edgesToDelete = new();

            public DialogueEdgeConnectorListener(DialogueGraph graph) => this.graph = graph;

            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                if (edge.output != null && edge.input == null) graph.OpenEdgeSearch(edge.output, position);
            }

            public void OnDrop(GraphView graphView, Edge edge)
            {
                if (edge.input == null || edge.output == null) return;

                edgesToDelete.Clear();

                if (edge.input.capacity == Port.Capacity.Single)
                {
                    foreach (Edge old in edge.input.connections)
                    {
                        if (old != edge) edgesToDelete.Add(old);
                    }
                }

                if (edge.output.capacity == Port.Capacity.Single)
                {
                    foreach (Edge old in edge.output.connections)
                    {
                        if (old != edge && !edgesToDelete.Contains(old)) edgesToDelete.Add(old);
                    }
                }

                if (edgesToDelete.Count > 0) graphView.DeleteElements(edgesToDelete);

                edgesToCreate.Clear();
                edgesToCreate.Add(edge);
                GraphViewChange change = new() { edgesToCreate = edgesToCreate };
                List<Edge> created = graphView.graphViewChanged != null ? graphView.graphViewChanged(change).edgesToCreate : edgesToCreate;

                if (created == null) return;

                for (int i = 0; i < created.Count; i++)
                {
                    Edge connection = created[i];
                    graphView.AddElement(connection);
                    connection.input.Connect(connection);
                    connection.output.Connect(connection);
                }
            }
        }
        #endregion

        [System.Serializable]
        private class CopyData
        {
            public string format;
            public List<NodeData> nodes = new();
            public List<LinkData> links = new();
        }
        #endregion
    }
}
