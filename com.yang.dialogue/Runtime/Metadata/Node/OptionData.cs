using System.Collections.Generic;

namespace Yang.Dialogue
{
    [System.Serializable]
    public struct OptionData
    {
        public string type;
        public List<GenericData> datas;

        public OptionData(string type)
        {
            this.type = type;

            datas = new();
        }
    }
}