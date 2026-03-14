using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    [System.Serializable]
    public struct NodeData : System.IEquatable<NodeData>
    {
        public string guid;
        public NodeType type;

        public List<DataWrapper> portDatas;
        public List<DataWrapper> optionDatas;

#if UNITY_EDITOR
        public Vector2 position;
#endif

        public NodeData(NodeType type)
        {
            guid = System.Guid.NewGuid().ToString();

            this.type = type;

            portDatas = new();
            optionDatas = new();

#if UNITY_EDITOR
            position = Vector2.zero;
#endif
        }

        public NodeData(NodeData data)
        {
            guid = System.Guid.NewGuid().ToString();

            type = data.type;

            portDatas = new();
            optionDatas = new();

            foreach (DataWrapper datas in data.portDatas) portDatas.Add(new(datas));
            foreach (DataWrapper datas in data.optionDatas) optionDatas.Add(new(datas));

#if UNITY_EDITOR
            position = data.position;
#endif
        }

        #region Equatable
        public readonly bool Equals(NodeData other) => guid == other.guid;

        public readonly override bool Equals(object obj) => obj is NodeData other && Equals(other);

        public readonly override int GetHashCode() => System.HashCode.Combine(guid);
        #endregion
    }
}