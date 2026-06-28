using System;
using System.Collections.Generic;
using UnityEngine.Localization.SmartFormat.Core.Extensions;

namespace Yang.Localize.Formatter
{
    public class Multiply : IFormatter
    {
        public string[] Names { get; set; } = { "mult" };

        public bool TryEvaluateFormat(IFormattingInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.FormatterOptions)) return false;

            decimal result;
            string[] options = info.FormatterOptions.Split('|');

            if (info.CurrentValue is IConvertible convertible)
            {
                if (!FormatterContext.TryParse(convertible, out result)) return false;

                for (int i = 0; i < options.Length; i++)
                {
                    if (FormatterContext.TryParse(options[i], out decimal value)) result *= value;
                    else return false;
                }

                FormatterContext.WriteResult(info, result);

                return true;
            }
            else if (info.CurrentValue is IReadOnlyList<float> items)
            {
                if (FormatterContext.TryParse(items, options[0], out decimal value)) result = value;
                else return false;

                for (int i = 1; i < options.Length; i++)
                {
                    if (FormatterContext.TryParse(items, options[i], out value)) result *= value;
                    else return false;
                }

                FormatterContext.WriteResult(info, result);

                return true;
            }

            return false;
        }
    }
}