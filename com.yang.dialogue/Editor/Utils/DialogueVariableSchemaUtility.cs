using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace Yang.Dialogue.Editor
{
    internal static class DialogueVariableSchemaUtility
    {
        internal readonly struct VariableInfo
        {
            public readonly string key;
            public readonly string label;
            public readonly Type type;

            public VariableInfo(string key, string label, Type type)
            {
                this.key = key;
                this.label = label;
                this.type = type;
            }
        }

        private sealed class Variable
        {
            public string key;
            public string label;
            public Type type;
            public int order;
        }

        private static List<Variable> variables;

        private static void Ensure()
        {
            if (variables != null) return;

            variables = new List<Variable>();
            HashSet<string> keys = new();

            foreach (Type type in TypeCache.GetTypesWithAttribute<DialogueVariableSchemaAttribute>())
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (field.IsStatic || (field.FieldType != typeof(float) && field.FieldType != typeof(bool))) continue;

                    DialogueVariableAttribute attribute = field.GetCustomAttribute<DialogueVariableAttribute>();

                    if (!keys.Add(field.Name))
                    {
                        UnityEngine.Debug.LogWarning($"Duplicate dialogue variable '{field.Name}' on {type.FullName}; it was ignored.");
                        continue;
                    }

                    variables.Add(new Variable
                    {
                        key = field.Name,
                        label = string.IsNullOrWhiteSpace(attribute?.DisplayName)
                            ? ObjectNames.NicifyVariableName(field.Name)
                            : attribute.DisplayName,
                        type = field.FieldType,
                        order = attribute?.Order ?? 0,
                    });
                }
            }

            variables.Sort((a, b) =>
            {
                int order = a.order.CompareTo(b.order);
                return order != 0 ? order : string.Compare(a.label, b.label, StringComparison.Ordinal);
            });
        }

        public static void GetKeys(List<string> target, Type valueType)
        {
            Ensure();
            target.Clear();
            target.Add("");

            foreach (Variable variable in variables)
            {
                if (variable.type == valueType) target.Add(variable.key);
            }
        }

        public static void GetKeys(List<string> target)
        {
            Ensure();
            target.Clear();
            target.Add("");

            foreach (Variable variable in variables) target.Add(variable.key);
        }

        public static Type GetValueType(string key)
        {
            Ensure();

            foreach (Variable variable in variables)
            {
                if (variable.key == key) return variable.type;
            }

            return null;
        }

        public static List<VariableInfo> GetVariables()
        {
            Ensure();

            List<VariableInfo> result = new(variables.Count);

            foreach (Variable variable in variables)
                result.Add(new VariableInfo(variable.key, variable.label, variable.type));

            return result;
        }

        public static void ShowMenu(Action<string, Type> onSelected)
        {
            GenericMenu menu = new();
            List<VariableInfo> items = GetVariables();

            if (items.Count == 0)
            {
                menu.AddDisabledItem(new UnityEngine.GUIContent("No [DialogueVariableSchema] variables"));
            }
            else
            {
                foreach (VariableInfo item in items)
                {
                    VariableInfo captured = item;
                    menu.AddItem(new UnityEngine.GUIContent(item.label), false,
                        () => onSelected(captured.key, captured.type));
                }
            }

            menu.ShowAsContext();
        }
    }
}
