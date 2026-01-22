using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    [System.Serializable]
    public class DialogueWrapper
    {
        public string key;

        public string currentNode;

        public readonly List<string> triggers = new();

        public void SetDatas(string key, string currentNode, IReadOnlyCollection<string> triggers)
        {
            this.key = key;

            this.currentNode = currentNode;

            this.triggers.Clear();

            foreach (string trigger in triggers) this.triggers.Add(trigger);
        }
    }
}