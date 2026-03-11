using System.Runtime.CompilerServices;
using UnityEngine;

[System.Serializable]
public struct GenericData
{
    public enum Type
    {
        Int,
        Float,
        Long,
        Bool,
        Color,
        Guid,
        Enum,
        String,
    }

    public Type type;

    public int intValue;
    public float floatValue;
    public long longValue;
    public bool boolValue;
    public Color colorValue;
    public string guidValue;
    public int enumValue;
    public string stringValue;

    public GenericData(Type type)
    {
        this.type = type;

        intValue = default;
        floatValue = default;
        longValue = default;
        boolValue = default;
        colorValue = default;
        guidValue = default;
        enumValue = default;
        stringValue = default;
    }

    public GenericData(string data)
    {
        intValue = default;
        floatValue = default;
        longValue = default;
        boolValue = default;
        colorValue = default;
        guidValue = default;
        enumValue = default;
        stringValue = data;

        if (int.TryParse(data, out int i))
        {
            type = Type.Int;

            intValue = i;
        }
        else if (float.TryParse(data, out float f))
        {
            type = Type.Float;

            floatValue = f;
        }
        else if (long.TryParse(data, out long l))
        {
            type = Type.Long;

            longValue = l;
        }
        else if (bool.TryParse(data, out bool b))
        {
            type = Type.Bool;

            boolValue = b;
        }
        else if (ColorUtility.TryParseHtmlString(data, out Color c))
        {
            type = Type.Color;

            colorValue = c;
        }
        else if (System.Guid.TryParse(data, out System.Guid g))
        {
            type = Type.Guid;

            guidValue = g.ToString();
        }
        else type = Type.String;
    }

    public GenericData(int value)
    {
        type = Type.Int;

        intValue = value;
        floatValue = default;
        longValue = default;
        boolValue = default;
        colorValue = default;
        guidValue = default;
        enumValue = default;
        stringValue = default;
    }

    public GenericData(float value)
    {
        type = Type.Float;

        intValue = default;
        floatValue = value;
        longValue = default;
        boolValue = default;
        colorValue = default;
        guidValue = default;
        enumValue = default;
        stringValue = default;
    }

    public GenericData(long value)
    {
        type = Type.Float;

        intValue = default;
        floatValue = default;
        longValue = value;
        boolValue = default;
        colorValue = default;
        guidValue = default;
        enumValue = default;
        stringValue = default;
    }

    public GenericData(bool value)
    {
        type = Type.Bool;

        intValue = default;
        floatValue = default;
        longValue = default;
        boolValue = value;
        colorValue = default;
        guidValue = default;
        enumValue = default;
        stringValue = default;
    }

    public GenericData(Color value)
    {
        type = Type.Color;

        intValue = default;
        floatValue = default;
        longValue = default;
        boolValue = default;
        colorValue = value;
        guidValue = default;
        enumValue = default;
        stringValue = default;
    }

    public GenericData(System.Guid value)
    {
        type = Type.Guid;

        intValue = default;
        floatValue = default;
        longValue = default;
        boolValue = default;
        colorValue = default;
        guidValue = value.ToString();
        enumValue = default;
        stringValue = default;
    }

    public GenericData(System.Enum value)
    {
        type = Type.Enum;

        intValue = default;
        floatValue = default;
        longValue = default;
        boolValue = default;
        colorValue = default;
        guidValue = default;
        enumValue = System.Convert.ToInt32(value);
        stringValue = default;
    }

    public readonly bool TryGetValue<T>(out T result)
    {
        switch (type)
        {
            case Type.Int:
                if (intValue is T iConvert)
                {
                    result = iConvert;

                    return true;
                }
                break;

            case Type.Float:
                if (floatValue is T fConvert)
                {
                    result = fConvert;

                    return true;
                }
                break;

            case Type.Long:
                if (longValue is T lConvert)
                {
                    result = lConvert;

                    return true;
                }
                break;

            case Type.Bool:
                if (boolValue is T bConvert)
                {
                    result = bConvert;

                    return true;
                }
                break;

            case Type.Color:
                if (colorValue is T cConvert)
                {
                    result = cConvert;

                    return true;
                }
                break;

            case Type.Guid:
                System.Guid.TryParse(guidValue, out System.Guid guid);

                if (guid is T gConvert)
                {
                    result = gConvert;

                    return true;
                }
                break;

            case Type.Enum:
                result = Unsafe.As<int, T>(ref Unsafe.AsRef(enumValue));

                return true;

            case Type.String:
                if (stringValue is T sConvert)
                {
                    result = sConvert;

                    return true;
                }
                break;
        }

        result = default;

        return false;
    }
}
