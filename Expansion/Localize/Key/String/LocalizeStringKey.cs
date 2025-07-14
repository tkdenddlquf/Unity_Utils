using System;
using Yang.Localize;

[Serializable]
public class LocalizeStringKey
{
    [Localize("table")]
    public string key;

    public static implicit operator string(LocalizeStringKey key) => key.key;
}