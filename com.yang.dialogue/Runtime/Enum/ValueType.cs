namespace Yang.Dialogue
{
    /// <summary>
    /// Data type of a dialogue value.
    /// </summary>
    public enum ValueType : byte
    {
        Float,
        Bool,
    }

    /// <summary>
    /// Operation used when assigning to a dialogue value.
    /// </summary>
    public enum ValueSetterType : byte
    {
        /// <summary>Adds to the current value.</summary>
        Plus,
        /// <summary>Subtracts from the current value.</summary>
        Minus,
        /// <summary>Overwrites the current value.</summary>
        Set,
    }

    /// <summary>
    /// Comparison operator used when checking a dialogue value.
    /// </summary>
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