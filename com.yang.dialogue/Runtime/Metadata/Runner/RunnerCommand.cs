using System.Collections.Generic;

namespace Yang.Dialogue
{
    /// <summary>
    /// Converts a serialized runner instruction into a game-defined command or event without reflection.
    /// </summary>
    public interface IDialogueInstruction
    {
        void ReadFrom(RunnerCommand command);
    }

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

        public bool TryGet(int fieldId, out GenericData value)
        {
            for (int i = 0; i < arguments.Count; i++)
            {
                if (arguments[i].FieldId == fieldId)
                {
                    value = arguments[i].Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public string GetString(int fieldId, string fallback = "")
            => TryGet(fieldId, out GenericData value) && value.TryGetString(out string result) ? result : fallback;

        public float GetFloat(int fieldId, float fallback = 0f)
            => TryGet(fieldId, out GenericData value) && value.TryGetFloat(out float result) ? result : fallback;

        public int GetInt(int fieldId, int fallback = 0)
            => TryGet(fieldId, out GenericData value) && value.TryGetInt(out int result) ? result : fallback;

        public long GetLong(int fieldId, long fallback = 0)
            => TryGet(fieldId, out GenericData value) && value.TryGetLong(out long result) ? result : fallback;

        public bool GetBool(int fieldId, bool fallback = false)
            => TryGet(fieldId, out GenericData value) && value.TryGetBool(out bool result) ? result : fallback;

        public UnityEngine.Color32 GetColor(int fieldId, UnityEngine.Color32 fallback = default)
            => TryGet(fieldId, out GenericData value) && value.TryGetColor(out UnityEngine.Color32 result) ? result : fallback;

        public System.Guid GetGuid(int fieldId, System.Guid fallback = default)
            => TryGet(fieldId, out GenericData value) && value.TryGetGuid(out System.Guid result) ? result : fallback;

        public T GetEnum<T>(int fieldId, T fallback = default) where T : struct, System.Enum
            => TryGet(fieldId, out GenericData value) && value.TryGetEnum(out T result) ? result : fallback;

        /// <summary>Converts this instruction without reflection after verifying its schema id.</summary>
        public bool TryConvert<T>(string expectedId, out T result) where T : class, IDialogueInstruction, new()
        {
            if (ID != expectedId)
            {
                result = default;
                return false;
            }

            result = new T();
            result.ReadFrom(this);
            return true;
        }
    }

    /// <summary>A stable-id, strongly tagged value passed to a dialogue command.</summary>
    public readonly struct RunnerArgument
    {
        public int FieldId { get; }
        public GenericData Value { get; }

        public RunnerArgument(int fieldId, GenericData value)
        {
            FieldId = fieldId;
            Value = value;
        }
    }
}
