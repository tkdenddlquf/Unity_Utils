using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Yang.Dialogue.Editor
{
    public class StartNode : BaseNode
    {
        public StartNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        public override void SetPorts()
        {
            CreatePort(Direction.Output, Port.Capacity.Single);
        }
    }
}