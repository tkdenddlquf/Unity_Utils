namespace Yang.Dialogue
{
    public readonly struct RunnerText
    {
        public readonly string table;
        public readonly string entry;

        public RunnerText(string table, string entry)
        {
            this.table = table;
            this.entry = entry;
        }
    }
}
