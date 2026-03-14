namespace Yang.Dialogue
{
    public struct RunnerText
    {
        public int portIndex;

        public string table;
        public string entry;

        public RunnerText(int portIndex, string table, string entry)
        {
            this.portIndex = portIndex;

            this.table = table;
            this.entry = entry;
        }

        public RunnerText(string table, string entry)
        {
            portIndex = 0;

            this.table = table;
            this.entry = entry;
        }
    }
}