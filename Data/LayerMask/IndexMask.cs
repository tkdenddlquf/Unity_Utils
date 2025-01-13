using System;
using UnityEngine;

[Serializable]
public struct IndexMask
{
    public LayerMask layerMask;

    public int Index { get; private set; }

    private string convertName;

    public static implicit operator IndexMask(LayerMask layerMask)
    {
        IndexMask result = default;

        result.Index = Convert(layerMask);
        result.convertName = layerMask.ToString();

        return result;
    }

    public static implicit operator int(IndexMask indexMask)
    {
        if (indexMask.convertName != indexMask.layerMask.ToString())
        {
            indexMask.Index = Convert(indexMask.layerMask);
            indexMask.convertName = indexMask.layerMask.ToString();
        }

        return indexMask.Index;
    }

    private static int Convert(LayerMask layerMask)
    {
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask.value & (1 << i)) != 0) return i;
        }

        return -1;
    }
}