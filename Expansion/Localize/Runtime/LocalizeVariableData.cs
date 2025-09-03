using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Yang.Localize
{
    public struct LocalizeVariableData
    {
        public LocalizeReference reference;

        public string key;
        public IVariable value;
    }
}