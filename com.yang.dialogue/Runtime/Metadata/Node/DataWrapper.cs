using System.Collections.Generic;

namespace Yang.Dialogue
{
    [System.Serializable]
    public class DataWrapper
    {
        public List<GenericData> data = new();

        public DataWrapper(DataWrapper wrapper)
        {
            data = new(wrapper.data);
        }

        public DataWrapper(List<GenericData> data)
        {
            this.data = data;
        }

        public DataWrapper(params GenericData[] data)
        {
            for (int i = 0; i < data.Length; i++) this.data.Add(data[i]);
        }
    }
}