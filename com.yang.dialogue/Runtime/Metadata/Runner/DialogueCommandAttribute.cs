using System;

namespace Yang.Dialogue
{
    /// <summary>
    /// Declares a data-only command schema that the dialogue editor can discover.
    /// Public instance fields become command arguments; the type is never stored in DialogueSO.
    /// </summary>
    public abstract class DialogueSchemaAttribute : Attribute
    {
        public string ID { get; }
        public string Menu { get; set; }

        protected DialogueSchemaAttribute(string id)
        {
            ID = id;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DialogueCommandAttribute : DialogueSchemaAttribute
    {
        public DialogueCommandAttribute(string id) : base(id) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DialogueEventAttribute : DialogueSchemaAttribute
    {
        public DialogueEventAttribute(string id) : base(id) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DialogueVariableSchemaAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class DialogueVariableAttribute : Attribute
    {
        public string DisplayName { get; }
        public int Order { get; set; }

        public DialogueVariableAttribute(string displayName = null)
        {
            DisplayName = displayName;
        }
    }

    /// <summary>Customizes the label and order of a public command schema field.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class DialogueArgumentAttribute : Attribute
    {
        public string DisplayName { get; }
        public int Order { get; set; }

        public DialogueArgumentAttribute(string displayName = null)
        {
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Replaces a string field with a popup populated by a static parameterless provider method.
    /// The method may return any IEnumerable&lt;string&gt;.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class DialogueOptionsAttribute : Attribute
    {
        public Type ProviderType { get; }
        public string MethodName { get; }

        public DialogueOptionsAttribute(Type providerType, string methodName)
        {
            ProviderType = providerType;
            MethodName = methodName;
        }
    }
}
