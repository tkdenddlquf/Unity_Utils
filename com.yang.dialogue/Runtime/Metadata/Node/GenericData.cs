using System;
using System.Globalization;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// Serializable tagged-union value that stores one of several primitive, Unity, or string types and exposes typed accessors.
    /// </summary>
    [Serializable]
    public struct GenericData
    {
        /// <summary>
        /// Enumerates which underlying kind a GenericData instance currently holds.
        /// </summary>
        public enum DataType : byte
        {
            Int,
            Float,
            Long,
            Bool,
            Color,
            Guid,
            Enum,
            Object,
            String
        }

        [SerializeField] private DataType type;

        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private long longValue;
        [SerializeField] private Color32 colorValue;

        [SerializeField] private UnityEngine.Object objectValue;
        [SerializeField] private string stringValue;

        /// <summary>
        /// The kind of value currently stored.
        /// </summary>
        public readonly DataType Type => type;

        /// <summary>
        /// Parses a string into a GenericData, inferring its type (int, long, float, bool, color, GUID) or falling back to a string.
        /// </summary>
        public static GenericData Convert(string data)
        {
            GenericData result = new();

            if (int.TryParse(data, out int i))
            {
                result.type = DataType.Int;
                result.intValue = i;

                return result;
            }

            if (long.TryParse(data, out long l))
            {
                result.type = DataType.Long;
                result.longValue = l;

                return result;
            }

            if (float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                result.type = DataType.Float;
                result.floatValue = f;

                return result;
            }

            if (bool.TryParse(data, out bool b))
            {
                result.type = DataType.Bool;
                result.intValue = b ? 1 : 0;

                return result;
            }

            if (ColorUtility.TryParseHtmlString(data, out Color c))
            {
                result.type = DataType.Color;
                result.colorValue = c;

                return result;
            }

            if (Guid.TryParse(data, out Guid g))
            {
                result.type = DataType.Guid;
                result.stringValue = g.ToString();

                return result;
            }

            result.type = DataType.String;
            result.stringValue = data;

            return result;
        }

        /// <summary>
        /// Creates an empty value tagged with the given type.
        /// </summary>
        public GenericData(DataType type)
        {
            this = default;

            this.type = type;
        }

        /// <summary>
        /// Creates a value holding an int.
        /// </summary>
        public GenericData(int value)
        {
            this = default;

            type = DataType.Int;

            intValue = value;
        }

        /// <summary>
        /// Creates a value holding a float.
        /// </summary>
        public GenericData(float value)
        {
            this = default;

            type = DataType.Float;

            floatValue = value;
        }

        /// <summary>
        /// Creates a value holding a long.
        /// </summary>
        public GenericData(long value)
        {
            this = default;

            type = DataType.Long;

            longValue = value;
        }

        /// <summary>
        /// Creates a value holding a bool.
        /// </summary>
        public GenericData(bool value)
        {
            this = default;

            type = DataType.Bool;

            intValue = value ? 1 : 0;
        }

        /// <summary>
        /// Creates a value holding a Color32.
        /// </summary>
        public GenericData(Color32 value)
        {
            this = default;

            type = DataType.Color;

            colorValue = value;
        }

        /// <summary>
        /// Creates a value holding a GUID (stored as its string form).
        /// </summary>
        public GenericData(Guid value)
        {
            this = default;

            type = DataType.Guid;

            stringValue = value.ToString();
        }

        /// <summary>
        /// Creates a value holding an enum (stored as its int value).
        /// </summary>
        public GenericData(Enum value)
        {
            this = default;

            type = DataType.Enum;

            intValue = System.Convert.ToInt32(value);
        }

        /// <summary>
        /// Creates a value holding a Unity object reference.
        /// </summary>
        public GenericData(UnityEngine.Object value)
        {
            this = default;

            type = DataType.Object;

            objectValue = value;
        }

        /// <summary>
        /// Creates a value holding a string.
        /// </summary>
        public GenericData(string value)
        {
            this = default;

            type = DataType.String;

            stringValue = value;
        }

        /// <summary>
        /// Outputs the stored int and returns true only when the value is of int type.
        /// </summary>
        public readonly bool TryGetInt(out int value)
        {
            if (type == DataType.Int)
            {
                value = intValue;

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Outputs the stored float and returns true only when the value is of float type.
        /// </summary>
        public readonly bool TryGetFloat(out float value)
        {
            if (type == DataType.Float)
            {
                value = floatValue;

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Outputs the stored long and returns true only when the value is of long type.
        /// </summary>
        public readonly bool TryGetLong(out long value)
        {
            if (type == DataType.Long)
            {
                value = longValue;

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Outputs the stored bool and returns true only when the value is of bool type.
        /// </summary>
        public readonly bool TryGetBool(out bool value)
        {
            if (type == DataType.Bool)
            {
                value = intValue == 1;

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Outputs the stored color and returns true only when the value is of color type.
        /// </summary>
        public readonly bool TryGetColor(out Color32 value)
        {
            if (type == DataType.Color)
            {
                value = colorValue;

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Outputs the stored GUID and returns true only when the value is a parseable GUID type.
        /// </summary>
        public readonly bool TryGetGuid(out Guid value)
        {
            if (type == DataType.Guid && Guid.TryParse(stringValue, out value)) return true;

            value = default;

            return false;
        }

        /// <summary>
        /// Outputs the stored enum as type T and returns true only when the value is of enum type.
        /// </summary>
        public readonly bool TryGetEnum<T>(out T value) where T : struct, Enum
        {
            if (type == DataType.Enum)
            {
                value = (T)Enum.ToObject(typeof(T), intValue);

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Outputs the stored Unity object and returns true only when the value is of object type.
        /// </summary>
        public readonly bool TryGetObject(out UnityEngine.Object value)
        {
            if (type == DataType.Object)
            {
                value = objectValue;

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Outputs the stored string and returns true only when the value is of string type.
        /// </summary>
        public readonly bool TryGetString(out string value)
        {
            if (type == DataType.String)
            {
                value = stringValue;

                return true;
            }

            value = default;

            return false;
        }

        /// <summary>
        /// Returns the stored int, or 0 if not an int.
        /// </summary>
        public readonly int GetInt() => type == DataType.Int ? intValue : 0;

        /// <summary>
        /// Returns the stored float, or 0 if not a float.
        /// </summary>
        public readonly float GetFloat() => type == DataType.Float ? floatValue : 0;

        /// <summary>
        /// Returns the stored long, or 0 if not a long.
        /// </summary>
        public readonly long GetLong() => type == DataType.Long ? longValue : 0;

        /// <summary>
        /// Returns the stored bool, or false if not a bool.
        /// </summary>
        public readonly bool GetBool() => type == DataType.Bool && (intValue == 1);

        /// <summary>
        /// Returns the stored color, or default if not a color.
        /// </summary>
        public readonly Color32 GetColor() => type == DataType.Color ? colorValue : default;

        /// <summary>
        /// Returns the stored GUID, or default if not a parseable GUID.
        /// </summary>
        public readonly Guid GetGuid() => type == DataType.Guid && Guid.TryParse(stringValue, out Guid value) ? value : default;

        /// <summary>
        /// Returns the stored enum as type T, or default if not an enum.
        /// </summary>
        public readonly T GetEnum<T>() where T : struct, Enum => type == DataType.Enum ? (T)Enum.ToObject(typeof(T), intValue) : default;

        /// <summary>
        /// Returns the stored Unity object, or default if not an object.
        /// </summary>
        public readonly UnityEngine.Object GetObject() => type == DataType.Object ? objectValue : default;

        /// <summary>
        /// Returns the stored string, or default if not a string.
        /// </summary>
        public readonly string GetString() => type == DataType.String ? stringValue : default;

        /// <summary>
        /// Returns the stored value rendered as a string according to its current type.
        /// </summary>
        public override string ToString()
        {
            return type switch
            {
                DataType.Int => intValue.ToString(),
                DataType.Float => floatValue.ToString(CultureInfo.InvariantCulture),
                DataType.Long => longValue.ToString(),
                DataType.Bool => (intValue == 1).ToString(),
                DataType.Color => colorValue.ToString(),
                DataType.Guid => stringValue,
                DataType.Enum => intValue.ToString(),
                DataType.Object => objectValue == null ? "" : objectValue.ToString(),
                DataType.String => stringValue,
                _ => ""
            };
        }
    }
}