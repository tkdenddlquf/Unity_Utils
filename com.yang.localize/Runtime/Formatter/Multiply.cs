using UnityEngine.Localization.SmartFormat.Core.Extensions;

namespace Yang.Localize.Formatter
{
    public class Multiply : IFormatter
    {
        public string[] Names { get; set; } = { "mult" };

        public bool TryEvaluateFormat(IFormattingInfo info)
        {
            if (!FormatterContext.TryGetProduct(info, out decimal result)) return false;
            FormatterContext.WriteResult(info, result);
            return true;
        }
    }
}
