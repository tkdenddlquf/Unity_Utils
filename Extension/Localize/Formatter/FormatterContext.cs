using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Extensions;
using UnityEngine.ResourceManagement.AsyncOperations;
using static UnityEngine.Rendering.DebugUI;

namespace Yang.Localize.Formatter
{
    public static class FormatterContext
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            RegisterAll();

            LocalizationSettings.InitializationOperation.Completed -= RegisterAll;
            LocalizationSettings.InitializationOperation.Completed += RegisterAll;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorInit()
        {
            RegisterAll();

            LocalizationSettings.InitializationOperation.Completed -= RegisterAll;
            LocalizationSettings.InitializationOperation.Completed += RegisterAll;
        }
#endif

        #region Register
        private static readonly Type[] types =
        {
            typeof(Multiply),
            typeof(FloorMultiply)
        };

        private static void RegisterAll(AsyncOperationHandle<LocalizationSettings> handle) => RegisterAll();

        private static void RegisterAll()
        {
            SmartFormatter smart = LocalizationSettings.StringDatabase.SmartFormatter;

            foreach (Type type in types)
            {
                if (smart.FormatterExtensions.Exists(f => f.GetType() == type)) continue;

                smart.AddExtensions((IFormatter)Activator.CreateInstance(type));
            }
        }
        #endregion

        #region Formatter
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static bool TryParse(IConvertible convertible, out decimal result)
        {
            switch (convertible)
            {
                case decimal d:
                    result = d;
                    return true;

                case int i:
                    result = i;
                    return true;

                case float f:
                    result = (decimal)f;
                    return true;

                case double db:
                    result = (decimal)db;
                    return true;

                default:
                    result = default;
                    return false;
            }
        }

        public static bool TryParse(string text, out decimal result)
        {
            if (decimal.TryParse(text, NumberStyles.Any, Culture, out result)) return true;

            return false;
        }

        public static bool TryParse(IReadOnlyList<float> items, string option, out decimal value)
        {
            if (decimal.TryParse(option, NumberStyles.Any, Culture, out value)) return true;

            if (option.Length >= 3 && option[0] == '[' && option[^1] == ']' &&
                int.TryParse(option[1..^1], out int index) && index >= 0 && index < items.Count)
            {
                value = (decimal)items[index];

                return true;
            }

            value = default;

            return false;
        }

        public static void WriteResult(IFormattingInfo info, decimal value) => info.Write(value.ToString(Culture));
        #endregion
    }
}
