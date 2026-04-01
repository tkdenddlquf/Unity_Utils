namespace Yang.Dialogue.Editor
{
    public class StartNode : BaseNode
    {
        public StartNode(DialogueEditorWindow window, string guid) : base(window, guid)
        {

        }

        public override void SetPorts()
        {
            SetDefault();

            CreateOutputPort();
        }

        private void SetDefault()
        {
            if (portDatas.Count == 0) portDatas.Add(new());
        }
    }
}