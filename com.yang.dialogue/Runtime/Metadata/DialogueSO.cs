using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    [System.Serializable, CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Node")]
    public class DialogueSO : ScriptableObject
    {
        public string key;

        public NodeData startNode;

        public List<EventKeySO> events;
        public List<ConditionKeySO> conditions;

        [SerializeField] private List<NodeData> nodes = new();
        [SerializeField] private List<LinkData> links = new();

        #region Node
        public NodeData GetNode(string guid)
        {
            if (guid == startNode.guid) return startNode;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].guid == guid) return nodes[i];
            }

            return default;
        }

        public IReadOnlyList<NodeData> GetNodes() => nodes;

        public bool TryGetNode(string guid, out NodeData node)
        {
            if (guid == startNode.guid)
            {
                node = startNode;

                return true;
            }
            else
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].guid == guid)
                    {
                        node = nodes[i];

                        return true;
                    }
                }
            }

            node = default;

            return false;
        }

        public void SetNode(string guid, NodeData data)
        {
            if (startNode.guid == guid) startNode = data;
            else
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].guid == guid) nodes[i] = data;
                }
            }
        }

        public void AddNode(NodeData data) => nodes.Add(data);

        public bool RemoveNode(NodeData data) => nodes.Remove(data);

        public bool RemoveNode(string guid)
        {
            NodeData data = GetNode(guid);

            return nodes.Remove(data);
        }
        #endregion

        #region Link
        public LinkData GetLink(string guid, string portName)
        {
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].nodeGuid == guid && links[i].portName == portName) return links[i];
            }

            return default;
        }

        public IEnumerable<LinkData> GetLinks(string guid)
        {
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].nodeGuid == guid) yield return links[i];
            }
        }

        public IReadOnlyList<LinkData> GetLinks() => links;

        public void AddLink(LinkData data) => links.Add(data);

        public bool RemoveLink(LinkData data) => links.Remove(data);

        public bool ContainsLink(LinkData data) => links.Contains(data);
        #endregion
    }
}