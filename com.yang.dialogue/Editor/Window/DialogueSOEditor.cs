using UnityEditor;
using UnityEngine;

namespace Yang.Dialogue.Editor
{
    [CustomEditor(typeof(DialogueSO))]
    public class DialogueSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            GUI.enabled = false;

            SerializedProperty script = serializedObject.FindProperty("m_Script");

            EditorGUILayout.PropertyField(script);

            GUI.enabled = true;

            SerializedProperty events = serializedObject.FindProperty("events");
            SerializedProperty conditions = serializedObject.FindProperty("conditions");

            EditorGUILayout.PropertyField(events);
            EditorGUILayout.PropertyField(conditions);

            serializedObject.ApplyModifiedProperties();
        }
    }
}