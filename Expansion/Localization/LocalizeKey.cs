using System;

[Serializable]
public class LocalizeKey
{
    [Localize("table")]
    public string key;

    public static implicit operator string(LocalizeKey key) => key.key;
}