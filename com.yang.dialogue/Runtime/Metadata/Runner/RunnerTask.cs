namespace Yang.Dialogue
{
    public struct RunnerTask
    {
        public string currentNode;

        public RunnerToken token;

        public RunnerTask(string currentNode)
        {
            this.currentNode = currentNode;

            token = new() { targetNode = currentNode };
        }

        public RunnerTask(RunnerToken token)
        {
            currentNode = token.PointNode;

            this.token = token;
        }
    }
}