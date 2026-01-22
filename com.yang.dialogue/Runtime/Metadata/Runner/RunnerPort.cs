using System;

namespace Yang.Dialogue
{
    public readonly struct RunnerPort : IEquatable<RunnerPort>
    {
        public readonly string guid;
        public readonly string portName;

        public RunnerPort(string guid, string portName)
        {
            this.guid = guid;
            this.portName = portName;
        }

        public readonly bool Equals(RunnerPort other) => guid == other.guid && portName == other.portName;

        public readonly override bool Equals(object obj) => obj is RunnerPort other && Equals(other);

        public readonly override int GetHashCode() => HashCode.Combine(guid, portName);
    }
}