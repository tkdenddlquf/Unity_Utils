namespace Yang.Dialogue
{
    public enum ValueType : byte
    {
        Float,
        Bool,
    }

    public enum ValueSetterType : byte
    {
        Plus,
        Minus,
        Set,
    }

    public enum ValueCheckType : byte
    {
        Less,
        Equal,
        LessEqual,
        Greater,
        NotEqual,
        GreaterEqual,
    }
}