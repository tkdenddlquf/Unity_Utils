using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

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

        public static void SetVariable(this LocalizeStringEvent stringEvent, LocalizeVariableData data)
        {
            stringEvent.Clear();

            stringEvent.SetVariable(data.key, data.value);
            stringEvent.SetTable(data.reference);
        }

        public static void SetVariable(this LocalizeStringEvent stringEvent, string key, IVariable value)
        {
            stringEvent.StringReference.Add(key, value);
        }

        public static string GetText(LocalizeReference reference) => LocalizationSettings.StringDatabase.GetLocalizedString(reference.tableName, reference.entryName);
    }
}