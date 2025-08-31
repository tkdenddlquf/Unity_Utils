using UnityEngine.Localization.SmartFormat.PersistentVariables;

namespace Yang.Localize
{
    public struct LocalizeVariableData
    {
        public string table;
        public string entry;

        public string key;
        public IVariable value;
    }
}