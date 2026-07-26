using System;
using System.Collections.Generic;
using System.Reflection;

namespace Yang.Dialogue
{
    /// <summary>Cached schema binding used by RunnerCommand.TryConvert.</summary>
    internal static class DialogueCommandBinding<T> where T : class, new()
    {
        private static readonly string id;
        private static readonly Dictionary<string, FieldInfo> fields = new();

        static DialogueCommandBinding()
        {
            Type type = typeof(T);

            DialogueSchemaAttribute schema = type.GetCustomAttribute<DialogueCommandAttribute>();

            schema ??= type.GetCustomAttribute<DialogueEventAttribute>();

            id = schema.ID;

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.IsStatic || field.IsNotSerialized || !IsSupported(field.FieldType)) continue;

                fields.Add(field.Name, field);
            }
        }

        public static bool TryConvert(RunnerCommand command, out T result)
        {
            if (command.ID != id)
            {
                result = default;
                return false;
            }

            result = new();

            foreach (RunnerArgument argument in command.Arguments)
            {
                if (fields.TryGetValue(argument.Key, out FieldInfo field)) field.SetValue(result, GetValue(field.FieldType, argument.Value));
            }

            return true;
        }

        private static object GetValue(Type type, GenericData data)
        {
            if (type == typeof(string)) return data.GetString();
            if (type == typeof(int)) return data.GetInt();
            if (type == typeof(float)) return data.GetFloat();
            if (type == typeof(bool)) return data.GetBool();

            return Enum.ToObject(type, int.Parse(data.ToString()));
        }

        private static bool IsSupported(Type type) => type == typeof(string) || type == typeof(int) || type == typeof(float) || type == typeof(bool) || type.IsEnum;
    }
}
