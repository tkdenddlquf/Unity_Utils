namespace Yang.Dialogue
{
    public struct RunnerText
    {
        public string portName;

        public string table;
        public string entry;

        public RunnerText(string portName, string table, string entry)
        {
            this.portName = portName;

            this.table = table;
            this.entry = entry;
        }

        public RunnerText(string table, string entry)
        {
            portName = "";

            this.table = table;
            this.entry = entry;
        }
    }
}