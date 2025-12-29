using System;
using System.Globalization;
using UnityEngine.Localization.SmartFormat.Core.Extensions;

namespace Yang.Localize
{
    public class MultiplyFormatter : IFormatter
    {
        public string[] Names { get; set; } = { "mult" };

        public bool TryEvaluateFormat(IFormattingInfo info)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            bool check = double.TryParse(info.FormatterOptions, NumberStyles.Any, culture, out double multiplier);

            if (!check) return false;

            if (info.CurrentValue is IConvertible v)
            {
                double result = Convert.ToDouble(v) * multiplier;

                info.Write(result.ToString(culture));

                return true;
            }

            return false;
        }
    }
}