using System;

namespace Yang.Localize.Editor
{
    public readonly struct EntryData : IEquatable<EntryData>
    {
        public readonly long id;
        public readonly string key;
        public readonly string preview;

        public EntryData(long id, string key, string preview = "")
        {
            this.id = id;
            this.key = key;
            this.preview = preview ?? "";
        }

        public string GetPreview(int maxLength = 90)
        {
            if (string.IsNullOrEmpty(preview)) return "";

            string text = preview.Replace('\n', ' ').Replace('\r', ' ');
            return text.Length > maxLength ? text.Substring(0, maxLength) + "…" : text;
        }

        public string GetDisplayName(int previewLength = 60)
        {
            string text = GetPreview(previewLength);
            return string.IsNullOrEmpty(text) ? key : $"{key}    {text}";
        }

        public override string ToString() => key;
        public bool Equals(EntryData other) => id == other.id;
        public override bool Equals(object obj) => obj is EntryData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(id);
    }
}
