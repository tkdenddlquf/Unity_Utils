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

        public void SetDatas(IReadOnlyDictionary<string, RunnerTask> tasks, IReadOnlyCollection<string> triggers)
        {
            keys.Clear();
            names.Clear();

            this.triggers.Clear();

            foreach (var task in tasks)
            {
                keys.Add(task.Key);
                names.Add(task.Value.currentNode);
            }

            foreach (string trigger in triggers) this.triggers.Add(trigger);
        }
    }
}