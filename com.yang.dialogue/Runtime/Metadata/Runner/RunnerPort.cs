using System;

namespace Yang.Dialogue
{
    /// <summary>
    /// Identifies a single node port by its owning node guid and port index; used as a key when resolving graph links.
    /// </summary>
    internal readonly struct RunnerPort : IEquatable<RunnerPort>
    {
        /// <summary>Guid of the node that owns this port.</summary>
        public readonly string guid;

        /// <summary>Index of this port on its node.</summary>
        public readonly int portIndex;

        /// <summary>Creates a port reference from the owning node guid and port index.</summary>
        public RunnerPort(string guid, int portIndex)
        {
            this.guid = guid;
            this.portIndex = portIndex;
        }

        /// <summary>Returns true if both ports share the same node guid and port index.</summary>
        public readonly bool Equals(RunnerPort other) => guid == other.guid && portIndex == other.portIndex;

        /// <summary>Returns true if the object is a RunnerPort equal to this one.</summary>
        public readonly override bool Equals(object obj) => obj is RunnerPort other && Equals(other);

        /// <summary>Returns a hash code combining the node guid and port index.</summary>
        public readonly override int GetHashCode() => HashCode.Combine(guid, portIndex);
    }
}