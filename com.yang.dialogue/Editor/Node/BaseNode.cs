using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    public abstract class BaseNode : Node
    {
        protected const int ITEM_MIN_WIDTH = 150;

        public string GUID { get; private set; }

        private readonly TextField idField;

        protected DialogueEditorWindow window;

        protected BaseNode(DialogueEditorWindow window, string guid)
        {
            this.window = window;

            GUID = guid;

            idField = GetGUIDField();

            contentContainer[0].Insert(1, idField);
        }

        public abstract void SetPorts();

        protected string CreatePortName(Direction direction = Direction.Output)
        {
            string portName = direction.ToString();

            for (int i = 0; ; i++)
            {
                bool find = true;

                foreach (VisualElement element in outputContainer.Children())
                {
                    if (element is Port port && port.portName == portName) find = false;
                }

                if (find) return portName;

                portName = $"{direction} {i + 1}";
            }
        }

        protected string CreateID(string baseID)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            string id = baseID;

            for (int i = 0; ; i++)
            {
                if (!data.ContainsOption(baseID, _ => _.Count != 0 && _[0].ToString() == id)) return id;

                id = $"{baseID} {i + 1}";
            }
        }

        protected Port CreatePort(Direction direction, Port.Capacity capacity, string portName = "")
        {
            Port port = InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(bool));

            if (portName == "") portName = CreatePortName(direction);

            port.portName = portName;

            switch (direction)
            {
                case Direction.Input:
                    inputContainer.Add(port);
                    break;

                case Direction.Output:
                    DialogueSO so = window.SO;
                    NodeData data = so.GetNode(GUID);

                    outputContainer.Add(port);

                    data.AddPort(portName);
                    so.SetNode(GUID, data);
                    break;
            }

            return port;
        }

        protected void RemovePort(Port port)
        {
            DialogueSO so = window.SO;
            NodeData data = so.GetNode(GUID);

            if (outputContainer.childCount > 1)
            {
                string portName = port.portName;
                LinkData link = so.GetLink(data.guid, portName);

                Undo.RecordObject(so, "Remove Port");

                window.RemoveEdge(port);

                int optionIndex = data.GetOptionIndex(_ => _.Count != 0 && _[0].ToString() == port.portName);

                data.RemoveAtOption(optionIndex);
                data.RemovePort(portName);

                so.RemoveLink(link);
                so.SetNode(GUID, data);

                outputContainer.Remove(port);

                RefreshPorts();
                RefreshExpandedState();

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private void ChangedCallback(ChangeEvent<string> evt)
        {
            DialogueSO so = window.SO;

            if (so.ContainsNode(evt.newValue)) idField.SetValueWithoutNotify(GUID);
            else
            {
                NodeData data = so.GetNode(GUID);

                Undo.RecordObject(so, $"Change GUID");

                data.guid = evt.newValue;

                so.SetNode(GUID, data);

                GUID = data.guid;

                EditorUtility.SetDirty(so);

                window.SetUnsaved();
            }
        }

        private TextField GetGUIDField()
        {
            TextField field = new("ID") { value = GUID };

            field.labelElement.style.minWidth = StyleKeyword.Auto;
            field.labelElement.style.width = StyleKeyword.Auto;

            field[1].style.minWidth = ITEM_MIN_WIDTH;

            field.RegisterValueChangedCallback(evt => ChangedCallback(evt));

            return field;
        }
    }
}