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

        protected Port CreateInputPort()
        {
            Port port = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));

            port.portName = "Input";

            inputContainer.Add(port);

            return port;
        }

        protected Port CreateOutputPort()
        {
            Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));

            port.portName = "Output";

            outputContainer.Add(port);

            return port;
        }

        protected void RemovePort(Port port)
        {
            DialogueSO so = window.SO;
            NodeData data = window.GetNode(GUID);

            if (data.portDatas.Count > 1)
            {
                int portIndex = port.parent.IndexOf(port);

                LinkData link = window.GetLink(data.guid, portIndex);

                Undo.RecordObject(so, "Remove Port");

                window.RemoveEdge(port);

                data.portDatas.RemoveAt(portIndex);

                window.Links.Remove(link);

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

            if (window.ContainsNode(evt.newValue)) idField.SetValueWithoutNotify(GUID);
            else
            {
                NodeData data = window.GetNode(GUID);

                Undo.RecordObject(so, "Change GUID");

                data.guid = evt.newValue;

                window.SetNode(GUID, data);

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