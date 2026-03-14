using System;
using System.Globalization;
using UnityEngine;

namespace Yang.Dialogue
{
    [Serializable]
    public struct GenericData
    {
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

        public readonly DataType Type => type;

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

        public GenericData(DataType type)
        {
            this = default;

            this.type = type;
        }

        public GenericData(int value)
        {
            this = default;

            type = DataType.Int;

            intValue = value;
        }

        public GenericData(float value)
        {
            this = default;

            type = DataType.Float;

            floatValue = value;
        }

        public GenericData(long value)
        {
            this = default;

            type = DataType.Long;

            longValue = value;
        }

        public GenericData(bool value)
        {
            this = default;

            type = DataType.Bool;

            intValue = value ? 1 : 0;
        }

        public GenericData(Color32 value)
        {
            this = default;

            type = DataType.Color;

            colorValue = value;
        }

        public GenericData(Guid value)
        {
            this = default;

            type = DataType.Guid;

            stringValue = value.ToString();
        }

        public GenericData(Enum value)
        {
            this = default;

            type = DataType.Enum;

            intValue = System.Convert.ToInt32(value);
        }

        public GenericData(UnityEngine.Object value)
        {
            this = default;

            type = DataType.Object;

            objectValue = value;
        }

        public GenericData(string value)
        {
            this = default;

            type = DataType.String;

            stringValue = value;
        }

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

        public readonly bool TryGetGuid(out Guid value)
        {
            if (type == DataType.Guid && Guid.TryParse(stringValue, out value)) return true;

            value = default;

            return false;
        }

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