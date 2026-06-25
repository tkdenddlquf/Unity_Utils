using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>Virtualized GraphView that hosts and edits a dialogue graph's nodes and links.</summary>
    public class DialogueGraph : GraphView
    {
        private readonly DialogueEditorWindow window;

        private string jsonCopyData = "";

        private const float NODE_MARGIN = 1500f;

        private readonly HashSet<string> visibleGuids = new();
        private readonly Dictionary<string, BaseNode> currentNodes = new();

        private readonly HashSet<string> selectedGuids = new();
        private bool suppressSelectionTracking;

        /// <summary>Builds the graph view, wires manipulators, copy/paste hooks, and sync callbacks.</summary>
        public DialogueGraph(DialogueEditorWindow window)
        {
            this.window = window;

            LoadStyleSheet();

            Insert(0, new GridBackground());

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            serializeGraphElements = SerializeNodes;

            unserializeAndPaste = UnserializeNode;

            canPasteSerializedData = CheckSerialize;

            viewTransformChanged += _ => SyncVisibleNodes();

            RegisterCallback<GeometryChangedEvent>(_ => SyncVisibleNodes());

            RegisterCallback<ValidateCommandEvent>(OnValidateCommand);
            RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);
        }

        /// <summary>Finds and applies the node stylesheet by asset name.</summary>
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
        /// <summary>Removes all elements then recreates only the currently visible ones.</summary>
        public void RebuildAll()
        {
            foreach (Node node in nodes.ToList()) RemoveElement(node);
            foreach (Edge edge in edges.ToList()) RemoveElement(edge);

            SyncVisibleNodes();
        }

        /// <summary>Schedules a visible-node sync for after the current change applies.</summary>
        public void RequestSync() => schedule.Execute(SyncVisibleNodes);

        /// <summary>Reconciles on-screen nodes so only those within the viewport (+margin) exist.</summary>
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

        /// <summary>Adds the node's guid to the visible set if its position falls inside the view rect.</summary>
        private void CollectVisible(NodeData data, Rect view)
        {
            if (!string.IsNullOrEmpty(data.guid) && view.Contains(data.position)) visibleGuids.Add(data.guid);
        }

        /// <summary>Rebuilds the guid-to-node lookup from the currently instantiated nodes.</summary>
        private void MapCurrentNodes()
        {
            currentNodes.Clear();

            foreach (Node node in nodes)
            {
                if (node is BaseNode baseNode) currentNodes[baseNode.GUID] = baseNode;
            }
        }

        /// <summary>Removes all edges and recreates those whose endpoints are both currently visible.</summary>
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

        private static readonly Color PortLinkedColor = new(0.36f, 0.82f, 0.47f);
        private static readonly Color PortEmptyColor = new(0.55f, 0.55f, 0.55f);

        private static readonly Color PortCapColor = new(0.48f, 0.48f, 0.48f);

        /// <summary>Recolors every visible node's port bars to show linked vs empty state.</summary>
        public void RefreshPortColors()
        {
            if (window.SO == null) return;

            List<LinkData> links = window.Links;

            foreach (Node node in nodes)
            {
                if (node is not BaseNode baseNode) continue;

                if (baseNode.inputContainer.childCount > 0 && baseNode.inputContainer[0] is Port inPort)
                {
                    ColorPort(inPort, IsInputLinked(links, baseNode.GUID));
                }

                for (int i = 0; i < baseNode.outputContainer.childCount; i++)
                {
                    if (baseNode.outputContainer[i] is Port outPort) ColorPort(outPort, IsOutputLinked(links, baseNode.GUID, i));
                }
            }
        }

        /// <summary>Sets a port's cap and accent bar to reflect its linked or empty state.</summary>
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

        /// <summary>Applies a uniform border width and color to all four sides of an element.</summary>
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

        /// <summary>Returns true if any link targets the given node guid.</summary>
        private static bool IsInputLinked(List<LinkData> links, string guid)
        {
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].targetGuid == guid) return true;
            }

            return false;
        }

        /// <summary>Returns true if a link originates from the given node guid and output port index.</summary>
        private static bool IsOutputLinked(List<LinkData> links, string guid, int portIndex)
        {
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].nodeGuid == guid && links[i].outPortIndex == portIndex) return true;
            }

            return false;
        }

        /// <summary>Pans (and optionally selects) so the node with the given guid is centered in view.</summary>
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

        /// <summary>Pans so a content-space rect (top-left plus size) is centered in the viewport.</summary>
        private void CenterOn(Vector2 topLeft, Vector2 size)
        {
            Vector3 scale = viewTransform.scale;

            Vector2 viewCenter = layout.size * 0.5f;
            Vector2 nodeCenter = topLeft + size * 0.5f;

            UpdateViewTransform(new Vector3(viewCenter.x - nodeCenter.x * scale.x, viewCenter.y - nodeCenter.y * scale.y, 0f), scale);
        }

        /// <summary>Returns the instantiated node with the given guid, or null if not on screen.</summary>
        private BaseNode FindVisibleNode(string guid)
        {
            foreach (Node node in nodes)
            {
                if (node is BaseNode baseNode && baseNode.GUID == guid) return baseNode;
            }

            return null;
        }

        /// <summary>Computes the content-space rect currently visible, expanded by a margin.</summary>
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
        /// <summary>Selects an element and tracks its guid in the logical selection.</summary>
        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);

            if (!suppressSelectionTracking && selectable is BaseNode node) selectedGuids.Add(node.GUID);
        }

        /// <summary>Deselects an element and drops its guid from the logical selection.</summary>
        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);

            if (!suppressSelectionTracking && selectable is BaseNode node) selectedGuids.Remove(node.GUID);
        }

        /// <summary>Clears the visible selection and the tracked logical selection.</summary>
        public override void ClearSelection()
        {
            base.ClearSelection();

            if (!suppressSelectionTracking) selectedGuids.Clear();
        }

        /// <summary>True when nodes are logically selected but none are currently on screen.</summary>
        private bool CanLogicalCopy()
        {
            if (selectedGuids.Count == 0) return false;

            foreach (ISelectable selectable in selection)
            {
                if (selectable is BaseNode) return false;
            }

            return true;
        }

        /// <summary>Claims the Copy command when only an off-screen logical selection exists.</summary>
        private void OnValidateCommand(ValidateCommandEvent evt)
        {
            if (evt.commandName == "Copy" && CanLogicalCopy()) evt.StopPropagation();
        }

        /// <summary>Handles the Copy command for an off-screen logical selection by writing the clipboard.</summary>
        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            if (evt.commandName == "Copy" && CanLogicalCopy())
            {
                EditorGUIUtility.systemCopyBuffer = BuildCopy();

                evt.StopPropagation();
            }
        }
        #endregion

        /// <summary>Builds the right-click menu with copy/paste/remove and node-add actions.</summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            DropdownMenu menu = evt.menu;

            menu.AppendAction("Copy", MenuActionCopy, selectedGuids.Count == 0 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Paste", MenuActionPaste, jsonCopyData == "" ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Remove", MenuActionRemove, selection.Count == 0 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);

            if (evt.target is Node) return;

            Vector2 mousePos = evt.localMousePosition;
            Vector2 nodePos = contentViewContainer.WorldToLocal(mousePos);

            menu.AppendSeparator();
            menu.AppendAction("Add Dialogue", _ => AddNode(NodeType.Dialogue, nodePos));
            menu.AppendAction("Add Choice", _ => AddNode(NodeType.Choice, nodePos));
            menu.AppendSeparator();
            menu.AppendAction("Add Trigger", _ => AddNode(NodeType.Trigger, nodePos));
            menu.AppendAction("Add Event", _ => AddNode(NodeType.Event, nodePos));
            menu.AppendAction("Add Object", _ => AddNode(NodeType.Object, nodePos));
            menu.AppendSeparator();
            menu.AppendAction("Add Condition", _ => AddNode(NodeType.Condition, nodePos));
            menu.AppendSeparator();
            menu.AppendAction("Add Wait", _ => AddNode(NodeType.Wait, nodePos));
        }

        /// <summary>Returns ports that can legally connect to the given start port.</summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            List<Port> compatiblePorts = new();

            foreach (var port in ports)
            {
                if (startPort.direction == port.direction) continue;
                if (startPort.node == port.node) continue;
                if (startPort.portType != port.portType) continue;

                compatiblePorts.Add(port);
            }

            return compatiblePorts;
        }

        /// <summary>Only deletes the selection when the graph itself holds focus.</summary>
        public override EventPropagation DeleteSelection()
        {
            Focusable focused = panel?.focusController?.focusedElement;

            if (focused == this) return base.DeleteSelection();
            else return EventPropagation.Stop;
        }

        #region Node
        /// <summary>Creates a new node of the given type at a position and records it for undo.</summary>
        private void AddNode(NodeType type, Vector2 position)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Node");

            NodeData data = new(type) { position = position };

            window.Nodes.Add(data);

            CreateNode(data);

            RefreshPortColors();

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        /// <summary>Instantiates a visual node from node data and adds it to the graph.</summary>
        public BaseNode CreateNode(NodeData data)
        {
            BaseNode node = ConvertData(data.type, data.guid);

            node.GraphPosition = data.position;

            node.SetPosition(new(data.position, Vector2.zero));
            node.SetPorts();

            if (data.expended) node.EnsureExtension();

            node.SetExpendedWithoutNotify(data.expended);

            node.RefreshExpandedState();

            AddElement(node);

            return node;
        }

        /// <summary>Constructs the concrete BaseNode subclass matching the node type and titles it.</summary>
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
                NodeType.Object => new ObjectNode(window, guid),
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
        /// <summary>GraphView serialize hook that returns the copy payload for the selection.</summary>
        private string SerializeNodes(IEnumerable<GraphElement> elements) => BuildCopy();

        /// <summary>Serializes the selected nodes and their internal links into a copy JSON string.</summary>
        private string BuildCopy()
        {
            DialogueSO so = window.SO;

            CopyData data = new();

            HashSet<string> guids = new();

            foreach (string guid in selectedGuids)
            {
                if (guid == so.StartGuid) continue;

                NodeData nodeData = window.GetNode(guid);

                if (string.IsNullOrEmpty(nodeData.guid)) continue;

                data.nodes.Add(nodeData);
                guids.Add(guid);
            }

            if (data.nodes.Count == 0)
            {
                jsonCopyData = "";

                return jsonCopyData;
            }

            List<LinkData> links = window.Links;

            for (int i = 0; i < links.Count; i++)
            {
                LinkData link = links[i];

                if (guids.Contains(link.nodeGuid) && guids.Contains(link.targetGuid)) data.links.Add(link);
            }

            jsonCopyData = JsonUtility.ToJson(data);

            return jsonCopyData;
        }

        /// <summary>Pastes copied nodes and links, offset to viewport center with remapped guids.</summary>
        private void UnserializeNode(string operationName, string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            CopyData copyData = JsonUtility.FromJson<CopyData>(data);

            if (copyData == null || copyData.nodes == null || copyData.nodes.Count == 0) return;

            ClearSelection();

            Vector2 nodeCenterPos = Vector2.zero;

            foreach (NodeData nodeData in copyData.nodes) nodeCenterPos += nodeData.position;

            nodeCenterPos /= copyData.nodes.Count;

            Vector2 newCenterPos = contentViewContainer.WorldToLocal(layout.size * 0.5f);
            Vector2 addedPos = newCenterPos - nodeCenterPos;

            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Paste Node");

            Dictionary<string, string> guidMap = new();

            foreach (NodeData nodeData in copyData.nodes)
            {
                NodeData newNodeData = new(nodeData);

                newNodeData.position += addedPos;

                guidMap[nodeData.guid] = newNodeData.guid;

                window.Nodes.Add(newNodeData);

                BaseNode node = CreateNode(newNodeData);

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

        /// <summary>Returns whether the given serialized data is non-empty and thus pasteable.</summary>
        private bool CheckSerialize(string data) => !string.IsNullOrEmpty(data);

        /// <summary>Context-menu Copy: stores the current selection as copy data.</summary>
        private void MenuActionCopy(DropdownMenuAction menuAction)
        {
            if (selectedGuids.Count == 0)
            {
                jsonCopyData = "";

                return;
            }

            BuildCopy();
        }

        /// <summary>Context-menu Paste: pastes the stored copy data.</summary>
        private void MenuActionPaste(DropdownMenuAction menuAction) => UnserializeNode("", jsonCopyData);

        /// <summary>Context-menu Remove: deletes the selected nodes from the graph and data.</summary>
        private void MenuActionRemove(DropdownMenuAction menuAction)
        {
            window.RemoveNode(selection);

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                if (selection[i] is BaseNode node)
                {
                    selectedGuids.Remove(node.GUID);

                    RemoveElement(node);
                }
            }
        }

        /// <summary>Serializable container holding copied nodes and their links.</summary>
        [System.Serializable]
        private class CopyData
        {
            public List<NodeData> nodes = new();
            public List<LinkData> links = new();
        }
        #endregion
    }
}