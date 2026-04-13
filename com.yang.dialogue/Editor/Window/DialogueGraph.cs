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
        }

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

            BaseNode node = CreateNode(data);

            AddElement(node);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        public BaseNode CreateNode(NodeData data)
        {
            BaseNode node = ConvertData(data.type, data.guid);

            node.SetPosition(new(data.position, Vector2.zero));
            node.SetPorts();
            node.SetExpendedWithoutNotify(data.expended);

            node.RefreshExpandedState();

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

                AddElement(node);
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