using System.Collections.Generic;

namespace Yang.Dialogue
{
    /// <summary>
    /// A serializable, asset-independent instruction emitted by a Command node.
    /// The dialogue package does not interpret the id or arguments; registered views do.
    /// </summary>
    public readonly struct RunnerCommand
    {
        private readonly IReadOnlyList<RunnerArgument> arguments;

        public string ID { get; }
        public IReadOnlyList<RunnerArgument> Arguments => arguments;

        public RunnerCommand(string id, IReadOnlyList<RunnerArgument> arguments)
        {
            ID = id;
            this.arguments = arguments;
        }

        public bool TryGet(string key, out GenericData value)
        {
            for (int i = 0; i < arguments.Count; i++)
            {
                if (arguments[i].Key == key)
                {
                    value = arguments[i].Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public string GetString(string key, string fallback = "")
            => TryGet(key, out GenericData value) && value.TryGetString(out string result) ? result : fallback;

        public float GetFloat(string key, float fallback = 0f)
            => TryGet(key, out GenericData value) && value.TryGetFloat(out float result) ? result : fallback;

        public int GetInt(string key, int fallback = 0)
            => TryGet(key, out GenericData value) && value.TryGetInt(out int result) ? result : fallback;

        public bool GetBool(string key, bool fallback = false)
            => TryGet(key, out GenericData value) && value.TryGetBool(out bool result) ? result : fallback;

        public T GetEnum<T>(string key, T fallback = default) where T : struct, System.Enum
            => TryGet(key, out GenericData value) && value.TryGetEnum(out T result) ? result : fallback;

        /// <summary>Converts this command to its schema type using a cached field binding.</summary>
        public bool TryConvert<T>(out T result) where T : class, new()
            => DialogueCommandBinding<T>.TryConvert(this, out result);
    }

    /// <summary>A named, strongly tagged value passed to a dialogue command.</summary>
    public readonly struct RunnerArgument
    {
        public string Key { get; }
        public GenericData Value { get; }

        public RunnerArgument(string key, GenericData value)
        {
            Key = key;
            Value = value;
        }
    }
}
