using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.Rendering.DebugUI;

namespace Yang.Localize
{
    public static class LocalizeDataManager
    {
        public static void SetTable(this LocalizeStringEvent stringEvent, LocalizeReference reference)
        {
            stringEvent.StringReference.SetReference(reference.tableName, reference.entryName);
            stringEvent.RefreshString();
        }

        public static void SetTable(this LocalizeSpriteEvent spriteEvent, LocalizeReference reference)
        {
            spriteEvent.AssetReference.SetReference(reference.tableName, reference.entryName);
        }

        public static void Clear(this LocalizeStringEvent stringEvent)
        {
            stringEvent.StringReference.Clear();
            stringEvent.StringReference.SetReference("", "");
            stringEvent.RefreshString();
        }

        public static void Clear(this LocalizeSpriteEvent spriteEvent)
        {
            spriteEvent.AssetReference.SetReference("", "");
        }

        public static void SetObjectVariable(this LocalizeStringEvent stringEvent, LocalizeVariableData data)
        {
            stringEvent.Clear();

            stringEvent.StringReference.Add(data.key, data.value);
            stringEvent.SetTable(data.reference);
        }

        public static void SetObjectVariable(this LocalizeStringEvent stringEvent, string key, IVariable value)
        {
            stringEvent.StringReference.Add(key, value);
        }

        public static void SetText(this TMPro.TMP_Text text, string table, string entry) => text.text = LocalizationSettings.StringDatabase.GetLocalizedString(table, entry);

        public static string GetText(string table, string entry) => LocalizationSettings.StringDatabase.GetLocalizedString(table, entry);
    }
}