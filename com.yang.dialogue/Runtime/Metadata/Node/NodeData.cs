using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    [Serializable]
    public struct NodeData : IEquatable<NodeData>
    {
        public string guid;
        public DialogueType.Node type;

        [SerializeField] private List<string> ports;
        [SerializeField] private List<OptionData> options;

#if UNITY_EDITOR
        public Vector2 position;
#endif

        public NodeData(DialogueType.Node type)
        {
            guid = Guid.NewGuid().ToString();

            this.type = type;

            ports = new();
            options = new();

#if UNITY_EDITOR
            position = Vector2.zero;
#endif
        }

        #region Port
        public readonly string GetPort(int index) => ports[index];

        public readonly IReadOnlyList<string> GetPorts() => ports;

        public readonly void AddPort(string portName)
        {
            if (!ports.Contains(portName)) ports.Add(portName);
        }

        public readonly bool RemovePort(string portName) => ports.Remove(portName);
        #endregion

        #region Option
        public readonly int OptionCount => options.Count;

        public readonly OptionData GetOption(int index) => options[index];

        public readonly IEnumerable<OptionData> GetOptions(string type, Func<List<string>, bool> comparer)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].type == type)
                {
                    if (comparer.Invoke(options[i].datas)) yield return options[i];
                }
            }
        }

        public readonly IReadOnlyList<OptionData> GetOptions() => options;

        public readonly int GetOptionIndex(string type, Func<List<string>, bool> comparer)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].type == type)
                {
                    if (comparer.Invoke(options[i].datas)) return i;
                }
            }

            return -1;
        }

        public readonly int GetOptionIndex(Func<List<string>, bool> comparer)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (comparer.Invoke(options[i].datas)) return i;
            }

            return -1;
        }

        public readonly void SetOption(int index, OptionData option) => options[index] = option;

        public readonly void InsertOption(int index, OptionData option) => options.Insert(index, option);

        public readonly void AddOption(OptionData option) => options.Add(option);

        public readonly void RemoveAtOption(int index) => options.RemoveAt(index);

        public readonly bool ContainsOption(string type, Func<List<string>, bool> comparer) => GetOptionIndex(type, comparer) != -1;
        #endregion

        #region Equatable
        public readonly bool Equals(NodeData other) => guid == other.guid;

        public readonly override bool Equals(object obj) => obj is NodeData other && Equals(other);

        public readonly override int GetHashCode() => HashCode.Combine(guid);
        #endregion
    }
}