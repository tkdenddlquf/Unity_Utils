using System;

namespace Yang.Dialogue
{
    /// <summary>
    /// Serializable data container describing a connection from one node's output port to a target node.
    /// </summary>
    [Serializable]
    public struct LinkData : IEquatable<LinkData>
    {
        public string nodeGuid;
        public string targetGuid;

        public int outPortIndex;

        #region Equatable
        /// <summary>
        /// Returns true when both links share the same source, target, and output port index.
        /// </summary>
        public readonly bool Equals(LinkData other)
        {
            return nodeGuid == other.nodeGuid &&
                   targetGuid == other.targetGuid &&
                   outPortIndex == other.outPortIndex;
        }

        /// <summary>
        /// Returns true when the object is a LinkData with equal source, target, and port index.
        /// </summary>
        public readonly override bool Equals(object obj) => obj is LinkData other && Equals(other);

        /// <summary>
        /// Returns a hash code combining source GUID, target GUID, and output port index.
        /// </summary>
        public readonly override int GetHashCode() => HashCode.Combine(nodeGuid, targetGuid, outPortIndex);
        #endregion
    }
}