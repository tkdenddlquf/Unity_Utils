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

            menu.AppendAction("Add Dialogue", _ => AddNode(DialogueType.Node.Dialogue, nodePos));
            menu.AppendAction("Add Condition", _ => AddNode(DialogueType.Node.Condition, nodePos));
            menu.AppendAction("Add Trigger", _ => AddNode(DialogueType.Node.Trigger, nodePos));
            menu.AppendAction("Add Event", _ => AddNode(DialogueType.Node.Event, nodePos));
            menu.AppendAction("Add Choice", _ => AddNode(DialogueType.Node.Choice, nodePos));
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
        private void AddNode(DialogueType.Node type, Vector2 position)
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

        public BaseNode CreateNode(DialogueType.Node type, string guid, Vector2 position) => CreateNode(ConvertData(type, guid), position);

        public BaseNode CreateNode(BaseNode node, Vector2 position)
        {
            if (window.SO == null) return null;

            node.SetPosition(new Rect(position, Vector2.zero));

            node.SetPorts();

            node.RefreshExpandedState();
            node.RefreshPorts();

            return node;
        }

        private BaseNode ConvertData(DialogueType.Node type, string guid)
        {
            BaseNode node = type switch
            {
                DialogueType.Node.Start => new StartNode(window, guid),
                DialogueType.Node.Dialogue => new DialogueNode(window, guid),
                DialogueType.Node.Condition => new ConditionNode(window, guid),
                DialogueType.Node.Trigger => new TriggerNode(window, guid),
                DialogueType.Node.Event => new EventNode(window, guid),
                DialogueType.Node.Choice => new ChoiceNode(window, guid),
                _ => null,
            };

            if (node != null) node.title = type.ToString();

            return node;
        }
        #endregion
    }
}