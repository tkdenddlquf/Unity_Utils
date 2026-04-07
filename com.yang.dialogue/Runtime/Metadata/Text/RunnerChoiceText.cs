namespace Yang.Dialogue
{
    public struct RunnerChoiceText
    {
        public int portIndex;

        public string table;
        public string entry;

        public RunnerChoiceText(int portIndex, string table, string entry)
        {
            this.portIndex = portIndex;

            this.table = table;
            this.entry = entry;
        }
    }
}