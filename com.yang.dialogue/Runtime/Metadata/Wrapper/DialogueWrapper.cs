using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// Serializable save/load payload capturing a dialogue runner's in-progress state (task keys, their point nodes, and runtime values) for persistence and restore.
    /// </summary>
    [System.Serializable]
    public class DialogueWrapper
    {
        [SerializeField] private List<string> keys = new();

        /// <summary>
        /// Saved task keys identifying each running dialogue task.
        /// </summary>
        public IReadOnlyList<string> Keys => keys;

        [SerializeField] private List<string> names = new();

        /// <summary>
        /// Saved point-node names paired with each task key by index.
        /// </summary>
        public IReadOnlyList<string> Names => names;

        [SerializeField] private List<RunnerValue> values = new();

        /// <summary>
        /// Saved runtime values captured from the runner.
        /// </summary>
        public IReadOnlyList<RunnerValue> Values => values;

        /// <summary>
        /// Captures the runner's current tasks (as key/point-node pairs) and runtime values into this payload, replacing any prior contents.
        /// </summary>
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