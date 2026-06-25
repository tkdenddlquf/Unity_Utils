namespace Yang.Dialogue
{
    /// <summary>
    /// Marker interface for condition keys. Implement this on a class that declares
    /// <c>const string</c> condition keys, then assign that class to the DialogueSO so
    /// Condition nodes can reference the keys it defines.
    /// </summary>
    public interface IConditionMarker
    {

    }
}