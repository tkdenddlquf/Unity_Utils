using System.Collections.Generic;

namespace Yang.Dialogue
{
    [System.Serializable]
    public struct DataWrapper
    {
        public List<GenericData> data;

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
            this.data = new(data);
        }
    }
}