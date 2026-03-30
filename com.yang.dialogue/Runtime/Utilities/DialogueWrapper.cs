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

        [SerializeField] private List<string> triggers = new();
        public IReadOnlyList<string> Triggers => triggers;

        internal void SetDatas(IReadOnlyDictionary<string, RunnerToken> tasks, IReadOnlyCollection<string> triggers)
        {
            keys.Clear();
            names.Clear();

            this.triggers.Clear();

            foreach (var task in tasks)
            {
                keys.Add(task.Key);
                names.Add(task.Value.PointNode);
            }

            foreach (string trigger in triggers) this.triggers.Add(trigger);
        }
    }
}