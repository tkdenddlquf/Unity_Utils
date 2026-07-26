using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class DialogueArgumentDrawerAttribute : Attribute
    {
        public Type AttributeType { get; }

        public DialogueArgumentDrawerAttribute(Type attributeType)
        {
            AttributeType = attributeType;
        }
    }

    /// <summary>Creates a custom UI Toolkit field for a dialogue schema field attribute.</summary>
    public abstract class DialogueArgumentDrawer
    {
        public abstract VisualElement CreateField(FieldInfo field, Attribute attribute, string label, GenericData value, Action<GenericData> onChanged);
    }
}
