using System;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Extensions;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Yang.Localize
{
    public static class AutoRegister
    {
        private static readonly Type[] types =
        {
            typeof(MultiplyFormatter),
            typeof(FloorMultiflyFormatter)
        };

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
    }
}
