using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    [System.Serializable]
    public struct NodeData : System.IEquatable<NodeData>
    {
        [SerializeField] private List<DataWrapper> portDatas;
        [SerializeField] private List<DataWrapper> optionDatas;

        public string guid;
        public NodeType type;
#if UNITY_EDITOR
        public Vector2 position;
        public bool expended;
#endif

        public readonly IReadOnlyList<DataWrapper> PortDatas => portDatas;
        public readonly IReadOnlyList<DataWrapper> OptionDatas => optionDatas;
#if UNITY_EDITOR
        public readonly List<DataWrapper> EditorPortDatas => portDatas;
        public readonly List<DataWrapper> EditorOptionDatas => optionDatas;
#endif

        public NodeData(NodeType type)
        {
            guid = System.Guid.NewGuid().ToString();
            this.type = type;
            portDatas = new();
            optionDatas = new();

#if UNITY_EDITOR
            position = Vector2.zero;
            expended = true;
#endif
        }

        public NodeData(NodeData data)
        {
            guid = System.Guid.NewGuid().ToString();
            type = data.type;
            portDatas = new();
            optionDatas = new();
            foreach (DataWrapper datas in data.portDatas)
                portDatas.Add(new(datas));
            foreach (DataWrapper datas in data.optionDatas)
                optionDatas.Add(new(datas));

#if UNITY_EDITOR
            position = data.position;
            expended = data.expended;
#endif
        }

        // Runtime nodes must not share mutable wrapper lists with the editable asset.
        internal readonly NodeData CreateRuntimeCopy()
        {
            NodeData copy = this;
            copy.portDatas = CopyWrappers(portDatas);
            copy.optionDatas = CopyWrappers(optionDatas);
            return copy;
        }

        internal readonly int GetRuntimeHashCode()
        {
            System.HashCode hash = new();
            hash.Add(guid);
            hash.Add(type);
            AddWrappersHash(ref hash, portDatas);
            AddWrappersHash(ref hash, optionDatas);
            return hash.ToHashCode();
        }

        private static List<DataWrapper> CopyWrappers(List<DataWrapper> source)
        {
            List<DataWrapper> result = new(source.Count);
            for (int i = 0; i < source.Count; i++)
                result.Add(new DataWrapper(source[i]));
            return result;
        }

        private static void AddWrappersHash(ref System.HashCode hash, List<DataWrapper> wrappers)
        {
            hash.Add(wrappers.Count);

            for (int i = 0; i < wrappers.Count; i++)
            {
                List<GenericData> values = wrappers[i].data;
                hash.Add(values.Count);
                for (int j = 0; j < values.Count; j++)
                    hash.Add(values[j].GetRuntimeHashCode());
            }
        }

        #region Equatable
        public readonly bool Equals(NodeData other) => guid == other.guid;

        public readonly override bool Equals(object obj) => obj is NodeData other && Equals(other);

        public readonly override int GetHashCode() => System.HashCode.Combine(guid);
        #endregion
    }
}
