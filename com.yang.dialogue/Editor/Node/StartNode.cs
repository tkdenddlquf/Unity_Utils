namespace Yang.Dialogue.Editor
{
    public class StartNode : BaseNode
    {
        public StartNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        public override void SetPorts()
        {
            CreateOutputPort();
        }
    }
}