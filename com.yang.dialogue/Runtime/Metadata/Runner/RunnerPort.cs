using System;

namespace Yang.Dialogue
{
    public readonly struct RunnerPort : IEquatable<RunnerPort>
    {
        public readonly string guid;
        public readonly int portIndex;

        public RunnerPort(string guid, int portIndex)
        {
            this.guid = guid;
            this.portIndex = portIndex;
        }

        public readonly bool Equals(RunnerPort other) => guid == other.guid && portIndex == other.portIndex;

        public readonly override bool Equals(object obj) => obj is RunnerPort other && Equals(other);

        public readonly override int GetHashCode() => HashCode.Combine(guid, portIndex);
    }
}