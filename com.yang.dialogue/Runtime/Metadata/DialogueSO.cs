using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Yang.Dialogue
{
    /// <summary>
    /// Serialized dialogue asset created via the Dialogue/Node menu; assign it to a DialogueRunner as the dialogue to play, and configure its event/condition markers and localization tables here.
    /// </summary>
    [System.Serializable, CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Node")]
    public class DialogueSO : ScriptableObject
    {
        [SerializeField] private NodeData startNode;

        /// <summary>
        /// GUID of the entry node where playback begins.
        /// </summary>
        public string StartGuid => startNode.guid;

        [SerializeReference] private IEventMarker events;

        /// <summary>
        /// Marker resolving event tokens raised by this dialogue.
        /// </summary>
        public IEventMarker Events => events;

        [SerializeReference] private IConditionMarker conditions;

        /// <summary>
        /// Marker evaluating condition tokens used by this dialogue.
        /// </summary>
        public IConditionMarker Conditions => conditions;

        [SerializeField] private LocalizedStringTable speakerTable;

        /// <summary>
        /// Localization table supplying speaker names.
        /// </summary>
        public LocalizedStringTable SpeakerTable => speakerTable;

        [SerializeField] private LocalizedStringTable textTable;

        /// <summary>
        /// Localization table supplying dialogue line text.
        /// </summary>
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

        /// <summary>
        /// Populates the given dictionaries with this asset's nodes keyed by GUID and its output-to-target port links for runtime traversal.
        /// </summary>
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

                links.TryAdd(output, input);
            }
        }
    }
}