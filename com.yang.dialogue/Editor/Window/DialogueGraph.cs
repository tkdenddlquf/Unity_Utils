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

        public DialogueGraph(DialogueEditorWindow window)
        {
            this.window = window;

            Insert(0, new GridBackground());

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is Node) return;

            Vector2 mousePos = evt.localMousePosition;
            Vector2 nodePos = contentViewContainer.WorldToLocal(mousePos);

            DropdownMenu menu = evt.menu;

            menu.AppendAction("Add Dialogue", _ => AddNode(NodeType.Dialogue, nodePos));
            menu.AppendAction("Add Condition", _ => AddNode(NodeType.Condition, nodePos));
            menu.AppendAction("Add Trigger", _ => AddNode(NodeType.Trigger, nodePos));
            menu.AppendAction("Add Event", _ => AddNode(NodeType.Event, nodePos));
            menu.AppendAction("Add Choice", _ => AddNode(NodeType.Choice, nodePos));
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

        #region Node
        private void AddNode(NodeType type, Vector2 position)
        {
            DialogueSO so = window.SO;

            Undo.RecordObject(so, "Create Node");

            NodeData data = new(type) { position = position };

            so.AddNode(data);

            BaseNode node = CreateNode(type, data.guid, position);

            AddElement(node);

            EditorUtility.SetDirty(so);

            window.SetUnsaved();
        }

        public BaseNode CreateNode(NodeType type, string guid, Vector2 position) => CreateNode(ConvertData(type, guid), position);

        public BaseNode CreateNode(BaseNode node, Vector2 position)
        {
            if (window.SO == null) return null;

            node.SetPosition(new Rect(position, Vector2.zero));

            node.SetPorts();

            node.RefreshExpandedState();
            node.RefreshPorts();

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
                _ => null,
            };

            if (node != null) node.title = type.ToString();

            return node;
        }
        #endregion
    }
}