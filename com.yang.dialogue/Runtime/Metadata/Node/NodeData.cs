using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// Serializable data container for a single dialogue node, holding its identity, type, and per-port/per-option payloads; identity is defined by its GUID.
    /// </summary>
    [System.Serializable]
    public struct NodeData : System.IEquatable<NodeData>
    {
        public string guid;
        public NodeType type;

        [SerializeField] private List<DataWrapper> portDatas;

        /// <summary>
        /// Read-only view of the node's per-port data payloads.
        /// </summary>
        public readonly IReadOnlyList<DataWrapper> PortDatas => portDatas;

        [SerializeField] private List<DataWrapper> optionDatas;

        /// <summary>
        /// Read-only view of the node's per-option data payloads.
        /// </summary>
        public readonly IReadOnlyList<DataWrapper> OptionDatas => optionDatas;

#if UNITY_EDITOR
        public Vector2 position;

        public bool expended;

        public readonly List<DataWrapper> EditorPortDatas => portDatas;
        public readonly List<DataWrapper> EditorOptionDatas => optionDatas;
#endif

        /// <summary>
        /// Creates a new node of the given type with a fresh GUID and empty data lists.
        /// </summary>
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

        /// <summary>
        /// Creates a deep copy of another node with a new GUID, cloning its port and option data.
        /// </summary>
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

            expended = data.expended;
#endif
        }

        #region Equatable
        /// <summary>
        /// Returns true when both nodes share the same GUID.
        /// </summary>
        public readonly bool Equals(NodeData other) => guid == other.guid;

        /// <summary>
        /// Returns true when the object is a NodeData with the same GUID.
        /// </summary>
        public readonly override bool Equals(object obj) => obj is NodeData other && Equals(other);

        /// <summary>
        /// Returns a hash code derived from the node's GUID.
        /// </summary>
        public readonly override int GetHashCode() => System.HashCode.Combine(guid);
        #endregion
    }
}