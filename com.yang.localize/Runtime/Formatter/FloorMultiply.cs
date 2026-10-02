using UnityEngine.Localization.SmartFormat.Core.Extensions;

namespace Yang.Localize.Formatter
{
    public class FloorMultiply : IFormatter
    {
        public string[] Names { get; set; } = { "floorMult" };

        public bool TryEvaluateFormat(IFormattingInfo info)
        {
            if (!FormatterContext.TryGetProduct(info, out decimal result)) return false;
            FormatterContext.WriteResult(info, decimal.Floor(result));
            return true;
        }
    }
}
