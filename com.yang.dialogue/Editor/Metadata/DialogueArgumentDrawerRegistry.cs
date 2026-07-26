using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    internal static class DialogueArgumentDrawerRegistry
    {
        private static Dictionary<Type, DialogueArgumentDrawer> drawers;

        public static bool TryCreate(FieldInfo field, string label, GenericData value, Action<GenericData> onChanged, out VisualElement element)
        {
            Ensure();

            foreach (Attribute attribute in field.GetCustomAttributes())
            {
                if (!drawers.TryGetValue(attribute.GetType(), out DialogueArgumentDrawer drawer)) continue;

                element = drawer.CreateField(field, attribute, label, value, onChanged);

                return true;
            }

            element = null;

            return false;
        }

        private static void Ensure()
        {
            if (drawers != null) return;

            drawers = new Dictionary<Type, DialogueArgumentDrawer>();

            foreach (Type type in TypeCache.GetTypesWithAttribute<DialogueArgumentDrawerAttribute>())
            {
                if (type.IsAbstract || !typeof(DialogueArgumentDrawer).IsAssignableFrom(type)) continue;

                DialogueArgumentDrawer drawer = Activator.CreateInstance(type) as DialogueArgumentDrawer;

                foreach (DialogueArgumentDrawerAttribute attribute in type.GetCustomAttributes<DialogueArgumentDrawerAttribute>())
                {
                    if (drawers.ContainsKey(attribute.AttributeType))
                    {
                        Debug.LogWarning($"Duplicate dialogue argument drawer for '{attribute.AttributeType.FullName}' on {type.FullName}; it was ignored.");

                        continue;
                    }

                    drawers.Add(attribute.AttributeType, drawer);
                }
            }
        }
    }
}
