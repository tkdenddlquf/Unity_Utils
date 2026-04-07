using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    [System.Serializable]
    public class DialogueWrapper
    {
        [SerializeField] private List<string> keys = new();
        public IReadOnlyList<string> Keys => keys;

        [SerializeField] private List<string> names = new();
        public IReadOnlyList<string> Names => names;

        [SerializeField] private List<RunnerValue> values = new();
        public IReadOnlyList<RunnerValue> Values => values;

        internal void SetDatas(IReadOnlyDictionary<string, RunnerToken> tasks, IReadOnlyCollection<RunnerValue> values)
        {
            keys.Clear();
            names.Clear();

            this.values.Clear();

            foreach (var task in tasks)
            {
                keys.Add(task.Key);
                names.Add(task.Value.PointNode);
            }

            foreach (RunnerValue value in values) this.values.Add(value);
        }
    }
}