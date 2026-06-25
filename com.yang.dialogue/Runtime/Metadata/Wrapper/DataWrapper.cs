using System.Collections.Generic;

namespace Yang.Dialogue
{
    /// <summary>
    /// Serializable container wrapping an ordered list of GenericData values for a single port or option.
    /// </summary>
    [System.Serializable]
    public struct DataWrapper
    {
        public List<GenericData> data;

        /// <summary>
        /// Creates a copy of another wrapper with its own list instance.
        /// </summary>
        public DataWrapper(DataWrapper wrapper)
        {
            data = new(wrapper.data);
        }

        /// <summary>
        /// Wraps the given list of values directly.
        /// </summary>
        public DataWrapper(List<GenericData> data)
        {
            this.data = data;
        }

        /// <summary>
        /// Wraps the given values into a new list.
        /// </summary>
        public DataWrapper(params GenericData[] data)
        {
            this.data = new(data);
        }
    }
}