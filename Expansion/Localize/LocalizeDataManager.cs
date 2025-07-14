using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

public static class LocalizeDataManager
{
    public static void SetTable(this LocalizeStringEvent stringEvent, string table, string entry)
    {
        stringEvent.StringReference.SetReference(table, entry);
        stringEvent.RefreshString();
    }

    public static void SetObjectVariable(this LocalizeStringEvent stringEvent, string name, ObjectVariable variable, Object value)
    {
        stringEvent.StringReference.Clear();
        stringEvent.SetTable("", "");

        variable.Value = value;

        stringEvent.StringReference.Add(name, variable);
    }

    public static void SetTable(this LocalizeSpriteEvent spriteEvent, string table, string entry)
    {
        spriteEvent.AssetReference.SetReference(table, entry);
    }

    public static void SetText(this TMPro.TMP_Text text, string table, string entry) => text.text = LocalizationSettings.StringDatabase.GetLocalizedString(table, entry);

    public static string GetText(string table, string entry) => LocalizationSettings.StringDatabase.GetLocalizedString(table, entry);
}
