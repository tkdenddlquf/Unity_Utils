using System;
using Yang.Localize;

[Serializable]
public class LocalizeAssetKey
{
    [Localize("assetTable")]
    public string key;

    public static implicit operator string(LocalizeAssetKey key) => key.key;
}