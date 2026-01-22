using System;

namespace Yang.Dialogue
{
    [Serializable]
    public struct LinkData : IEquatable<LinkData>
    {
        public string nodeGuid;
        public string portName;

        public string targetGuid;
        public string targetPortName;

        #region Equatable
        public readonly bool Equals(LinkData other)
        {
            return nodeGuid == other.nodeGuid &&
                   portName == other.portName &&
                   targetGuid == other.targetGuid &&
                   targetPortName == other.targetPortName;
        }

        public readonly override bool Equals(object obj) => obj is LinkData other && Equals(other);

        public readonly override int GetHashCode() => HashCode.Combine(nodeGuid, portName, targetGuid, targetPortName);
        #endregion
    }
}