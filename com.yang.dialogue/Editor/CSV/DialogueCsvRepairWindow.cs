using UnityEditor;
using UnityEngine;

namespace Yang.Dialogue.Editor
{
    internal sealed class DialogueCsvRepairWindow : EditorWindow
    {
        internal enum Decision
        {
            Cancel,
            Apply,
            Skip,
        }

        private string node;
        private string field;
        private string issue;
        private string value;
        private Decision decision;

        internal static Decision Request(int line, string nodeId, string nodeType, string fieldName, string currentValue, string description, out string corrected)
        {
            DialogueCsvRepairWindow window = CreateInstance<DialogueCsvRepairWindow>();
            window.titleContent = new GUIContent("Repair Dialogue CSV");
            window.minSize = new Vector2(510, 245);
            window.maxSize = new Vector2(720, 330);
            window.node = $"CSV row {line}  ·  {nodeType}  ·  ID: {(string.IsNullOrEmpty(nodeId) ? "(missing)" : nodeId)}";
            window.field = fieldName;
            window.issue = description;
            window.value = currentValue ?? "";
            window.decision = Decision.Cancel;
            window.ShowModalUtility();
            corrected = window.value;
            Decision result = window.decision;
            DestroyImmediate(window);

            return result;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(node, EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(issue, MessageType.Warning);
            EditorGUILayout.Space(5);
            value = EditorGUILayout.TextField($"Correct {field}", value);
            EditorGUILayout.Space(12);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply correction", GUILayout.Height(30))) Finish(Decision.Apply);

                if (GUILayout.Button("Skip row / link", GUILayout.Height(30))) Finish(Decision.Skip);

                if (GUILayout.Button("Cancel import", GUILayout.Height(30))) Finish(Decision.Cancel);
            }
        }

        private void Finish(Decision result)
        {
            decision = result;
            Close();
        }
    }
}
