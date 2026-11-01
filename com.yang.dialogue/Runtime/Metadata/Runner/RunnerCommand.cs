using System;
using System.Collections.Generic;

namespace Yang.Dialogue
{
    // The generated factories materialize instructions once when a graph is compiled.
    public readonly struct RunnerCommand
    {
        private readonly IReadOnlyList<RunnerArgument> arguments;
        private readonly object instruction;

        public string ID { get; }
        public IReadOnlyList<RunnerArgument> Arguments => arguments;

        public RunnerCommand(string id, IReadOnlyList<RunnerArgument> arguments, bool isEvent)
        {
            ID = id;
            this.arguments = arguments;
            instruction = null;
            instruction = DialogueInstructionRegistry.Create(id, isEvent, this);
        }

        public bool TryGet(int fieldId, out GenericData value)
        {
            for (int i = 0; i < arguments.Count; i++)
            {
                if (arguments[i].FieldId != fieldId) continue;

                value = arguments[i].Value;
                return true;
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

        public Guid GetGuid(int fieldId, Guid fallback = default)
            => TryGet(fieldId, out GenericData value) && value.TryGetGuid(out Guid result) ? result : fallback;

        public int GetEnumInt(int fieldId, int fallback = 0)
            => TryGet(fieldId, out GenericData value) && value.TryGetEnumInt(out int result) ? result : fallback;

        public bool TryConvert<T>(out T result) where T : class
        {
            result = instruction as T;
            return result != null;
        }
    }

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
