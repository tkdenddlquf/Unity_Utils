public interface ILocalizeFormatter
{
    public string Table { get; }
    public SerializeDict<string[], LocalizeStringKey> KeyDict { get; }
}
