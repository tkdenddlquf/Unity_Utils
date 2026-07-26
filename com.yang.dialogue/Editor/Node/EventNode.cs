using System;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>Schema-driven event node. Data layout matches CommandNode.</summary>
    public class EventNode : CommandNode
    {
        protected override Type SchemaAttributeType => typeof(DialogueEventAttribute);

        public EventNode(DialogueEditorWindow window, string guid) : base(window, guid) { }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target != this) return;

            evt.menu.AppendAction("Add Event", _ => CreateCommand());
            evt.menu.AppendSeparator();
        }
    }
}
