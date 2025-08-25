using UnityEngine.Localization.SmartFormat.Core.Extensions;

public class CustomFormatter : FormatterBase
{
    public override string[] DefaultNames => new string[] { "Trigger" };

    public override bool TryEvaluateFormat(IFormattingInfo formatInfo)
    {
        if (formatInfo.CurrentValue is ILocalizeFormatter info)
        {
            if (int.TryParse(formatInfo.FormatterOptions, out int index))
            {
                foreach (string key in info.KeyDict[index].key)
                {
                    if (!StepManager.Instance.GetMark(key)) return true;
                }

                formatInfo.Write(LocalizeDataManager.GetText(info.Table, info.KeyDict[index].value));

                return true;
            }
        }

        return false;
    }
}