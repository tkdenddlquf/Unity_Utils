using System;

namespace Yang.Dialogue
{
    [Serializable]
    public struct LinkData : IEquatable<LinkData>
    {
        public string nodeGuid;
        public string targetGuid;

        public int outPortIndex;

        #region Equatable
        public readonly bool Equals(LinkData other)
        {
            return nodeGuid == other.nodeGuid &&
                   targetGuid == other.targetGuid &&
                   outPortIndex == other.outPortIndex;
        }

        public readonly override bool Equals(object obj) => obj is LinkData other && Equals(other);

        public readonly override int GetHashCode() => HashCode.Combine(nodeGuid, targetGuid, outPortIndex);
        #endregion
    }
}