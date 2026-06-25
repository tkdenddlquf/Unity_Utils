namespace Yang.Dialogue
{
    /// <summary>
    /// Identifies the kind of node in a dialogue graph.
    /// </summary>
    public enum NodeType
    {
        /// <summary>Entry point of the dialogue graph.</summary>
        Start,
        Dialogue,
        /// <summary>Branches the flow based on a condition.</summary>
        Condition,
        /// <summary>Fires a trigger action.</summary>
        Trigger,
        /// <summary>Raises an event for listeners to handle.</summary>
        Event,
        /// <summary>Presents selectable options to the player.</summary>
        Choice,
        /// <summary>Pauses the flow for a duration or until a signal.</summary>
        Wait,
        /// <summary>References or acts on a scene object.</summary>
        Object,
    }
}