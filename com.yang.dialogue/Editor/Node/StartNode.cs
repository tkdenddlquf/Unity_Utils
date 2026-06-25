using UnityEditor.Experimental.GraphView;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Entry node of a dialogue graph; non-deletable and exposes a single output port.
    /// </summary>
    public class StartNode : BaseNode
    {
        /// <summary>Creates the start node and strips its deletable capability.</summary>
        public StartNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {
            capabilities &= ~Capabilities.Deletable;
        }

        /// <summary>Ensures default data exists, then creates the output port.</summary>
        public override void SetPorts()
        {
            SetDefault();

            CreateOutputPort();
        }

        /// <summary>Seeds a single default port-data entry when none exist.</summary>
        private void SetDefault()
        {
            if (portDatas.Count == 0) portDatas.Add(new());
        }
    }
}