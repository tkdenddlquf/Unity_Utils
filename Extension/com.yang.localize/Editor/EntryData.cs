using System;

namespace Yang.Localize.Editor
{
    public readonly struct EntryData : IEquatable<EntryData>
    {
        public readonly long id;
        public readonly string key;

        public readonly string tooltip;

        public EntryData(long id, string key, string tooltip)
        {
            this.id = id;
            this.key = key;

            this.tooltip = tooltip;
        }

        public EntryData(string id, string key)
        {
            long.TryParse(id, out this.id);
            this.key = key;

            tooltip = "";
        }

        public override string ToString() => key;

        #region Equatable
        public readonly bool Equals(EntryData other) => key == other.key || id == other.id;

        public readonly override bool Equals(object obj) => obj is EntryData other && Equals(other);

        public readonly override int GetHashCode() => HashCode.Combine(key);
        #endregion
    }
}