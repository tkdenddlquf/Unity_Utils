using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Yang.Localize
{
    public static class LocalizeDataManager
    {
        public static void SetTable(this LocalizeStringEvent stringEvent, string table, string entry)
        {
            stringEvent.StringReference.SetReference(table, entry);
            stringEvent.RefreshString();
        }

        public static void SetTable(this LocalizeSpriteEvent spriteEvent, string table, string entry)
        {
            spriteEvent.AssetReference.SetReference(table, entry);
        }

        public static void Clear(this LocalizeStringEvent stringEvent)
        {
            stringEvent.StringReference.Clear();
            stringEvent.SetTable("", "");
        }

        public static void Clear(this LocalizeSpriteEvent spriteEvent)
        {
            spriteEvent.SetTable("", "");
        }

        public static void SetObjectVariable(this LocalizeStringEvent stringEvent, LocalizeVariableData data)
        {
            stringEvent.Clear();

            stringEvent.StringReference.Add(data.key, data.value);
            stringEvent.SetTable(data.table, data.entry);
        }

        public static void SetObjectVariable(this LocalizeStringEvent stringEvent, string key, IVariable value)
        {
            stringEvent.StringReference.Add(key, value);
        }

        public static void SetText(this TMPro.TMP_Text text, string table, string entry) => text.text = LocalizationSettings.StringDatabase.GetLocalizedString(table, entry);

        public static string GetText(string table, string entry) => LocalizationSettings.StringDatabase.GetLocalizedString(table, entry);
    }
}