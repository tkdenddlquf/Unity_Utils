using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    [System.Serializable]
    public struct OptionData
    {
        public string type;
        public List<string> datas;

        public OptionData(string type)
        {
            this.type = type;

            datas = new();
        }
    }
}