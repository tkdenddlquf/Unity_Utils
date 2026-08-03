using System;

namespace Yang.Dialogue
{
    /// <summary>
    /// Declares a data-only command schema that the dialogue editor can discover.
    /// Supported public instance fields marked with DialogueArgument become command arguments;
    /// the schema type itself is never stored in DialogueSO.
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
        public int FieldId { get; }
        public string DisplayName { get; }

        public DialogueVariableAttribute(int fieldId, string displayName = null)
        {
            FieldId = fieldId;
            DisplayName = displayName;
        }
    }

    /// <summary>Assigns a stable numeric id and editor label to a public command or event field.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class DialogueArgumentAttribute : Attribute
    {
        public int FieldId { get; }
        public string DisplayName { get; }

        public DialogueArgumentAttribute(int fieldId, string displayName = null)
        {
            FieldId = fieldId;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Shows a command or event argument in the editor only when another field has the expected value.
    /// Hidden arguments remain serialized and are still available at runtime.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public sealed class DialogueShowIfAttribute : Attribute
    {
        public int FieldId { get; }
        public object ExpectedValue { get; }

        public DialogueShowIfAttribute(int fieldId, object expectedValue)
        {
            FieldId = fieldId;
            ExpectedValue = expectedValue;
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
