using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Yang.Dialogue
{
    [System.Serializable, CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Node")]
    public class DialogueSO : ScriptableObject
    {
        [Header("Graph")]
        [SerializeField] private NodeData startNode;
        [SerializeField] private List<NodeData> nodes = new();
        [SerializeField] private List<LinkData> links = new();

        [Header("Localization")]
        [SerializeField] private LocalizedStringTable speakerTable;
        [SerializeField] private LocalizedStringTable textTable;
#if UNITY_EDITOR
        [Header("Editor View")]
        [SerializeField] private Vector3 position = Vector3.zero;
        [SerializeField] private Vector3 scale = Vector3.one;
#endif

        public string StartGuid => startNode.guid;
        public LocalizedStringTable SpeakerTable => speakerTable;
        public LocalizedStringTable TextTable => textTable;
#if UNITY_EDITOR
        public Vector3 EditorPosition => position;
        public Vector3 EditorScale => scale;
        public NodeData EditorStartNode
        {
            get => startNode;
            set => startNode = value;
        }

        public List<NodeData> EditorNodes => nodes;
        public List<LinkData> EditorLinks => links;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(startNode.guid)) return;

            startNode = new NodeData(NodeType.Start);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        internal int GetRuntimeContentHash()
        {
            System.HashCode hash = new();
            hash.Add(startNode.GetRuntimeHashCode());
            hash.Add(nodes.Count);

            for (int i = 0; i < nodes.Count; i++) hash.Add(nodes[i].GetRuntimeHashCode());

            hash.Add(links.Count);

            for (int i = 0; i < links.Count; i++)
            {
                LinkData link = links[i];
                hash.Add(link.nodeGuid);
                hash.Add(link.targetGuid);
                hash.Add(link.outPortIndex);
            }

            return hash.ToHashCode();
        }

        internal void GetDatas(Dictionary<string, NodeData> nodes, Dictionary<RunnerPort, RunnerPort> links)
        {
            nodes.Clear();
            links.Clear();
            nodes.Add(startNode.guid, startNode.CreateRuntimeCopy());

            foreach (NodeData node in this.nodes) nodes.Add(node.guid, node.CreateRuntimeCopy());

            foreach (LinkData link in this.links)
            {
                RunnerPort output = new(link.nodeGuid, link.outPortIndex);
                RunnerPort input = new(link.targetGuid, -1);

                links.TryAdd(output, input);
            }
        }
    }
}
