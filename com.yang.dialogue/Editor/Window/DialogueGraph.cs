using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public class DialogueGraph : GraphView
    {
        private readonly DialogueEditorWindow window;

        private string jsonCopyData = "";

        private const float NODE_MARGIN = 1500f;

        private readonly HashSet<string> visibleGuids = new();
        private readonly Dictionary<string, BaseNode> currentNodes = new();

        // Selection tracked by guid (the "logical" selection) so nodes culled off-screen by the
        // virtualizer stay selected and can still be copied. While the virtualizer adds/removes the
        // on-screen elements it sets suppressSelectionTracking so those churn events don't mutate it.
        private readonly HashSet<string> selectedGuids = new();
        private bool suppressSelectionTracking;

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

            // Lets Ctrl+C work even when every selected node is currently culled off-screen
            // (GraphView's own copy is gated on the visible selection).
            RegisterCallback<ValidateCommandEvent>(OnValidateCommand);
            RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);
        }

        /// <summary>Loads the node theme stylesheet by name so it survives the package being moved.</summary>
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
        /// <summary>Full rebuild: removes every element, then creates only the on-screen nodes.</summary>
        public void RebuildAll()
        {
            foreach (Node node in nodes.ToList()) RemoveElement(node);
            foreach (Edge edge in edges.ToList()) RemoveElement(edge);

            SyncVisibleNodes();
        }

        /// <summary>Schedules a reconcile after the current change (e.g. node removal) is applied.</summary>
        public void RequestSync() => schedule.Execute(SyncVisibleNodes);

        /// <summary>
        /// Instantiates only the nodes inside the viewport (+margin); off-screen nodes are never
        /// created. Called on pan/zoom so nodes stream in and out, keeping large graphs cheap.
        /// </summary>
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

            // The element churn below would otherwise clobber the logical (guid) selection.
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

                    // Re-show the selection highlight for nodes that are logically selected.
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

        /// <summary>True when there is a logical (guid) selection but nothing copiable is on screen —
        /// the case GraphView's built-in Copy can't see, so we handle it ourselves.</summary>
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
            // Marking the command handled is what makes the editor send the matching ExecuteCommand.
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

        public override EventPropagation DeleteSelection()
        {
            Focusable focused = panel?.focusController?.focusedElement;

            if (focused == this) return base.DeleteSelection();
            else return EventPropagation.Stop;
        }

        #region Node
        private void AddNode(NodeType type, Vector2 position)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Node");

            NodeData data = new(type) { position = position };

            window.Nodes.Add(data);

            CreateNode(data);

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
        private string SerializeNodes(IEnumerable<GraphElement> elements) => BuildCopy();

        /// <summary>
        /// Serializes the logically selected nodes (tracked by guid, so nodes culled off-screen by
        /// the virtualizer are still included) plus every link whose endpoints are both inside the
        /// selection, so connections between copied nodes survive a paste.
        /// </summary>
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

            // Old guid -> new guid, so links between the pasted nodes can be remapped below.
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

        private bool CheckSerialize(string data) => !string.IsNullOrEmpty(data);

        private void MenuActionCopy(DropdownMenuAction menuAction)
        {
            if (selectedGuids.Count == 0)
            {
                jsonCopyData = "";

                return;
            }

            BuildCopy();
        }

        private void MenuActionPaste(DropdownMenuAction menuAction) => UnserializeNode("", jsonCopyData);

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

        [System.Serializable]
        private class CopyData
        {
            public List<NodeData> nodes = new();
            public List<LinkData> links = new();
        }
        #endregion
    }
}