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

        private const float CULL_MARGIN = 1200f;

        private readonly HashSet<VisualElement> culled = new();
        private readonly HashSet<BaseNode> visibleNodes = new();

        public DialogueGraph(DialogueEditorWindow window)
        {
            this.window = window;

            Insert(0, new GridBackground());

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            serializeGraphElements = SerializeNodes;

            unserializeAndPaste = UnserializeNode;

            canPasteSerializedData = CheckSerialize;

            viewTransformChanged += _ => CullElements();

            RegisterCallback<GeometryChangedEvent>(_ => CullElements());
        }

        #region Cull
        /// <summary>
        /// Hides nodes/edges outside the viewport (plus a margin) so off-screen elements aren't
        /// laid out or rendered. Conservative: culls by content-space position only.
        /// </summary>
        public void CullElements()
        {
            Rect rect = contentRect;

            if (rect.width <= 1 || rect.height <= 1) return;

            Vector3 translate = contentViewContainer.transform.position;
            Vector3 scale = contentViewContainer.transform.scale;

            if (scale.x == 0 || scale.y == 0) return;

            float minX = (0 - translate.x) / scale.x - CULL_MARGIN;
            float maxX = (rect.width - translate.x) / scale.x + CULL_MARGIN;
            float minY = (0 - translate.y) / scale.y - CULL_MARGIN;
            float maxY = (rect.height - translate.y) / scale.y + CULL_MARGIN;

            visibleNodes.Clear();

            foreach (Node node in nodes)
            {
                if (node is BaseNode baseNode)
                {
                    Vector2 p = baseNode.GraphPosition;

                    bool visible = p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;

                    SetCulled(baseNode, !visible);

                    if (visible) visibleNodes.Add(baseNode);
                }
            }

            foreach (Edge edge in edges)
            {
                bool outVisible = edge.output?.node is BaseNode outNode && visibleNodes.Contains(outNode);
                bool inVisible = edge.input?.node is BaseNode inNode && visibleNodes.Contains(inNode);

                // Hide unless both endpoints are visible (a culled endpoint has no layout to draw to).
                SetCulled(edge, !(outVisible && inVisible));
            }
        }

        private void SetCulled(VisualElement element, bool cull)
        {
            if (cull)
            {
                if (culled.Add(element)) element.style.display = DisplayStyle.None;
            }
            else if (culled.Remove(element)) element.style.display = DisplayStyle.Flex;
        }

        public void ResetCull()
        {
            culled.Clear();
            visibleNodes.Clear();
        }
        #endregion

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            DropdownMenu menu = evt.menu;

            menu.AppendAction("Copy", MenuActionCopy, selection.Count == 0 ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
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

            if (node != null) node.title = type.ToString();

            return node;
        }
        #endregion

        #region Action
        private string SerializeNodes(IEnumerable<GraphElement> elements)
        {
            DialogueSO so = window.SO;

            CopyData data = new();

            foreach (GraphElement element in elements)
            {
                if (element is BaseNode node)
                {
                    NodeData nodeData = window.GetNode(node.GUID);

                    if (nodeData.guid == so.StartGuid) continue;

                    data.nodes.Add(nodeData);
                }
            }

            if (data.nodes.Count == 0) jsonCopyData = "";
            else jsonCopyData = JsonUtility.ToJson(data);

            return jsonCopyData;
        }

        private void UnserializeNode(string operationName, string data)
        {
            if (data == "") return;

            CopyData copyData = JsonUtility.FromJson<CopyData>(data);

            ClearSelection();

            Vector2 nodeCenterPos = Vector2.zero;

            foreach (NodeData nodeData in copyData.nodes) nodeCenterPos += nodeData.position;

            nodeCenterPos /= copyData.nodes.Count;

            Vector2 newCenterPos = contentViewContainer.WorldToLocal(layout.size * 0.5f);
            Vector2 addedPos = newCenterPos - nodeCenterPos;

            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Paste Node");

            foreach (NodeData nodeData in copyData.nodes)
            {
                NodeData newNodeData = new(nodeData);

                newNodeData.position += addedPos;

                window.Nodes.Add(newNodeData);

                BaseNode node = CreateNode(newNodeData);

                AddToSelection(node);
            }

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        private bool CheckSerialize(string data) => !string.IsNullOrEmpty(data);

        private void MenuActionCopy(DropdownMenuAction menuAction)
        {
            if (selection.Count == 0)
            {
                jsonCopyData = "";

                return;
            }

            DialogueSO so = window.SO;

            CopyData data = new();

            foreach (ISelectable element in selection)
            {
                if (element is BaseNode node)
                {
                    NodeData nodeData = window.GetNode(node.GUID);

                    if (nodeData.guid == so.StartGuid) continue;

                    data.nodes.Add(nodeData);
                }
            }

            if (data.nodes.Count == 0) jsonCopyData = "";
            else jsonCopyData = JsonUtility.ToJson(data);
        }

        private void MenuActionPaste(DropdownMenuAction menuAction) => UnserializeNode("", jsonCopyData);

        private void MenuActionRemove(DropdownMenuAction menuAction)
        {
            window.RemoveNode(selection);

            for (int i = selection.Count - 1; i >= 0; i--)
            {
                if (selection[i] is BaseNode node) RemoveElement(node);
            }
        }

        [System.Serializable]
        private class CopyData
        {
            public List<NodeData> nodes = new();
        }
        #endregion
    }
}