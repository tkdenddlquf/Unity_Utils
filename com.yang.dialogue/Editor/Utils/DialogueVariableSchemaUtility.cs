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
            public readonly int fieldId;
            public readonly string label;
            public readonly Type type;

            public VariableInfo(int fieldId, string label, Type type)
            {
                this.fieldId = fieldId;
                this.label = label;
                this.type = type;
            }
        }

        private sealed class Variable
        {
            public int fieldId;
            public string label;
            public Type type;
        }

        private static List<Variable> variables;

        public static void Invalidate() => variables = null;

        private static void Ensure()
        {
            if (variables != null) return;

            variables = new List<Variable>();
            HashSet<int> fieldIds = new();

            foreach (Type type in TypeCache.GetTypesWithAttribute<DialogueVariableSchemaAttribute>())
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (field.IsStatic || (field.FieldType != typeof(float) && field.FieldType != typeof(bool))) continue;

                    DialogueVariableAttribute attribute = field.GetCustomAttribute<DialogueVariableAttribute>();

                    if (attribute == null || attribute.FieldId <= 0 || !fieldIds.Add(attribute.FieldId))
                    {
                        UnityEngine.Debug.LogWarning($"Dialogue variable '{type.FullName}.{field.Name}' has a missing, invalid, or duplicate FieldId; it was ignored.");
                        continue;
                    }

                    variables.Add(new Variable
                    {
                        fieldId = attribute.FieldId,
                        label = string.IsNullOrWhiteSpace(attribute.DisplayName)
                            ? ObjectNames.NicifyVariableName(field.Name)
                            : attribute.DisplayName,
                        type = field.FieldType,
                    });
                }
            }

            variables.Sort((a, b) => a.fieldId.CompareTo(b.fieldId));
        }

        public static void GetKeys(List<string> target, Type valueType)
        {
            Ensure();
            target.Clear();
            target.Add("");

            foreach (Variable variable in variables)
            {
                if (variable.type == valueType) target.Add(GetDisplayLabel(variable));
            }
        }

        public static void GetKeys(List<string> target)
        {
            Ensure();
            target.Clear();
            target.Add("");

            foreach (Variable variable in variables) target.Add(GetDisplayLabel(variable));
        }

        public static Type GetValueType(int fieldId)
        {
            Ensure();

            foreach (Variable variable in variables)
            {
                if (variable.fieldId == fieldId) return variable.type;
            }

            return null;
        }

        public static List<VariableInfo> GetVariables()
        {
            Ensure();

            List<VariableInfo> result = new(variables.Count);

            foreach (Variable variable in variables)
                result.Add(new VariableInfo(variable.fieldId, GetDisplayLabel(variable), variable.type));

            return result;
        }

        public static int GetFieldId(string displayLabel)
        {
            Ensure();

            foreach (Variable variable in variables)
            {
                if (GetDisplayLabel(variable) == displayLabel) return variable.fieldId;
            }

            return 0;
        }

        public static string GetLabel(int fieldId)
        {
            Ensure();

            foreach (Variable variable in variables)
            {
                if (variable.fieldId == fieldId) return GetDisplayLabel(variable);
            }

            return "";
        }

        private static string GetDisplayLabel(Variable variable) => $"{variable.label}  [{variable.fieldId}]";

        public static void ShowMenu(Action<int, Type> onSelected)
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
                        () => onSelected(captured.fieldId, captured.type));
                }
            }

            menu.ShowAsContext();
        }
    }
}
