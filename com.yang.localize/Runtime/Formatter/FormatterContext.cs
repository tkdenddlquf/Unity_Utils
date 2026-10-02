using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Extensions;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Yang.Localize.Formatter
{
    public static class FormatterContext
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit() => Initialize();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorInit() => Initialize();
#endif

        private static void Initialize()
        {
            AsyncOperationHandle<LocalizationSettings> operation = LocalizationSettings.InitializationOperation;
            if (operation.IsDone) RegisterAll();
            else
            {
                operation.Completed -= RegisterAll;
                operation.Completed += RegisterAll;
            }
        }

        private static void RegisterAll(AsyncOperationHandle<LocalizationSettings> _) => RegisterAll();

        private static void RegisterAll()
        {
            SmartFormatter smart = LocalizationSettings.StringDatabase.SmartFormatter;
            Register<Multiply>(smart);
            Register<FloorMultiply>(smart);
        }

        private static void Register<T>(SmartFormatter smart) where T : IFormatter, new()
        {
            if (smart.FormatterExtensions.Exists(formatter => formatter is T)) return;
            smart.AddExtensions(new T());
        }

        public static bool TryGetProduct(IFormattingInfo info, out decimal result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(info.FormatterOptions)) return false;

            string[] options = info.FormatterOptions.Split('|');
            int startIndex;
            bool useListValues;
            IReadOnlyList<float> items = info.CurrentValue as IReadOnlyList<float>;

            if (items != null)
            {
                if (!TryParse(items, options[0], out result)) return false;
                startIndex = 1;
                useListValues = true;
            }
            else if (info.CurrentValue is IConvertible convertible)
            {
                if (!TryParse(convertible, out result)) return false;
                startIndex = 0;
                useListValues = false;
            }
            else return false;

            for (int i = startIndex; i < options.Length; i++)
            {
                decimal value;
                bool parsed = useListValues ? TryParse(items, options[i], out value) : TryParse(options[i], out value);
                if (!parsed || !TryMultiply(ref result, value)) return false;
            }

            return true;
        }

        public static bool TryParse(IConvertible convertible, out decimal result)
        {
            result = default;
            if (convertible == null) return false;

            switch (convertible.GetTypeCode())
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    try
                    {
                        result = convertible.ToDecimal(Culture);
                        return true;
                    }
                    catch (OverflowException)
                    {
                        return false;
                    }
                default:
                    return false;
            }
        }

        public static bool TryParse(string text, out decimal result) => decimal.TryParse(text, NumberStyles.Any, Culture, out result);

        public static bool TryParse(IReadOnlyList<float> items, string option, out decimal value)
        {
            option = option?.Trim();
            if (decimal.TryParse(option, NumberStyles.Any, Culture, out value)) return true;

            bool isIndex = !string.IsNullOrEmpty(option) && option.Length >= 3 && option[0] == '[' && option[^1] == ']';
            if (isIndex && int.TryParse(option[1..^1], NumberStyles.Integer, Culture, out int index) && index >= 0 && index < items.Count)
            {
                try
                {
                    value = (decimal)items[index];
                    return true;
                }
                catch (OverflowException)
                {
                    value = default;
                    return false;
                }
            }

            value = default;
            return false;
        }

        private static bool TryMultiply(ref decimal result, decimal value)
        {
            try
            {
                result *= value;
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public static void WriteResult(IFormattingInfo info, decimal value) => info.Write(value.ToString(Culture));
    }
}
