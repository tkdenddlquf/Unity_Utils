using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Yang.Dialogue
{
    [System.Serializable, CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Node")]
    public class DialogueSO : ScriptableObject
    {
        [SerializeField] private NodeData startNode;
        public string StartGuid => startNode.guid;

        [SerializeReference] private IEventMarker events;
        public IEventMarker Events => events;

        [SerializeReference] private IConditionMarker conditions;
        public IConditionMarker Conditions => conditions;

        [SerializeField] private LocalizedStringTable speakerTable;
        public LocalizedStringTable SpeakerTable => speakerTable;

        [SerializeField] private LocalizedStringTable textTable;
        public LocalizedStringTable TextTable => textTable;

        [SerializeField] private List<NodeData> nodes = new();
        [SerializeField] private List<LinkData> links = new();

#if UNITY_EDITOR
        public Vector3 position = Vector3.zero;
        public Vector3 scale = Vector3.one;

        public NodeData EditorStartNode { get => startNode; set => startNode = value; }
        public List<NodeData> EditorNodes => nodes;
        public List<LinkData> EditorLinks => links;
#endif

        internal void GetDatas(Dictionary<string, NodeData> nodes, Dictionary<RunnerPort, RunnerPort> links)
        {
            nodes.Clear();
            links.Clear();

            nodes.Add(startNode.guid, startNode);

            foreach (NodeData node in this.nodes) nodes.Add(node.guid, node);

            foreach (LinkData link in this.links)
            {
                RunnerPort output = new(link.nodeGuid, link.outPortIndex);
                RunnerPort input = new(link.targetGuid, -1);

                links.Add(output, input);
            }
        }
    }
}