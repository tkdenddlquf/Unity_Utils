using System;
using System.Collections.Generic;
using System.Reflection;

namespace Yang.Dialogue.Editor
{
    /// <summary>
    /// Extracts string key constants declared on a marker object's type via reflection.
    /// </summary>
    public static class KeyConverter
    {
        /// <summary>Fills the list with every public static string const declared on the marker's type.</summary>
        public static void GetKeys(object marker, List<string> keys)
        {
            if (keys == null) return;

            keys.Clear();

            if (marker == null) return;

            Type type = marker.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];

                if (!field.IsLiteral) continue;
                if (field.IsInitOnly) continue;
                if (field.FieldType != typeof(string)) continue;

                keys.Add((string)field.GetRawConstantValue());
            }
        }
    }
}