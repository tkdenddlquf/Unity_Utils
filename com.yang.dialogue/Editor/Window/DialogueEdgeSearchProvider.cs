using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Yang.Dialogue.Editor
{
    internal sealed class DialogueEdgeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private DialogueGraph graph;
        private DialogueEditorWindow window;

        public void Initialize(DialogueGraph graph, DialogueEditorWindow window)
        {
            this.graph = graph;
            this.window = window;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> entries = new()
            {
                new SearchTreeGroupEntry(new GUIContent("Connect Output"), 0),
                new SearchTreeGroupEntry(new GUIContent("Create Node"), 1),
            };

            AddCreateEntry(entries, NodeType.Dialogue);
            AddCreateEntry(entries, NodeType.Choice);
            AddCreateEntry(entries, NodeType.Condition);
            AddCreateEntry(entries, NodeType.Trigger);
            AddCreateEntry(entries, NodeType.Event);
            AddCreateEntry(entries, NodeType.Command);
            AddCreateEntry(entries, NodeType.Wait);

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Connect Existing"), 1));

            if (window.SO == null) return entries;

            List<NodeData> nodes = window.Nodes;

            for (int i = 0; i < nodes.Count; i++)
            {
                NodeData node = nodes[i];

                if (string.IsNullOrEmpty(node.guid) || node.type == NodeType.Start) continue;

                entries.Add(new SearchTreeEntry(new GUIContent(window.GetConnectionSearchName(node)))
                {
                    level = 2,
                    userData = node.guid,
                });
            }

            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            graph.CompleteEdgeSearch(searchTreeEntry.userData);
            return true;
        }

        private static void AddCreateEntry(List<SearchTreeEntry> entries, NodeType type)
        {
            entries.Add(new SearchTreeEntry(new GUIContent(type.ToString()))
            {
                level = 2,
                userData = type,
            });
        }
    }
}
