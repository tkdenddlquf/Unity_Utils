using System;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
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
