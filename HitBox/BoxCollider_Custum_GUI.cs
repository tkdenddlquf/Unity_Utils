#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoxCollider_Custum))]
public class BoxCollider_Custum_GUI : Editor
{
    private GUIMode mode;
    private BoxCollider_Custum component;

    private void OnEnable()
    {
        component = target as BoxCollider_Custum;
    }

    public override void OnInspectorGUI()
    {
        if (component.data.Count == 0)
        {
            component.data.Add(new());

            Record("Component as New");
        }

        component.Select = EditorGUILayout.IntSlider("Select", component.Select, 0, component.data.Count);
        component[GUIMode.Pos] = EditorGUILayout.Vector3Field("Pos", component[GUIMode.Pos]);
        component[GUIMode.Scale] = EditorGUILayout.Vector3Field("Scale", component[GUIMode.Scale]);

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Pos"))
        {
            if (mode == GUIMode.Pos) mode = GUIMode.None;
            else mode = GUIMode.Pos;
        }

        if (GUILayout.Button("Scale"))
        {
            if (mode == GUIMode.Scale) mode = GUIMode.None;
            else mode = GUIMode.Scale;
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Add"))
        {
            component.data.Add(new());

            component.Select = component.data.Count - 1;

            Record("Add");
        }

        if (GUILayout.Button("Remove"))
        {
            if (component.Select == component.data.Count - 1) component.Select--;

            component.data.RemoveAt(component.Select + 1);

            Record("Remove");
        }

        GUILayout.EndHorizontal();

        if (GUI.changed) Record("Chaged");
    }

    private void OnSceneGUI()
    {
        switch (mode)
        {
            case GUIMode.None:
                break;

            case GUIMode.Pos:
                Tools.current = Tool.None;

                component[mode] = Handles.PositionHandle(component.transform.position + component[GUIMode.Pos], component.transform.rotation) - component.transform.position;
                break;

            case GUIMode.Scale:
                Tools.current = Tool.None;

                component[mode] = Handles.ScaleHandle(component[mode], component.transform.position + component[GUIMode.Pos], component.transform.rotation);
                break;
        }
    }

    private void Record(string _title)
    {
        EditorUtility.SetDirty(target);

        Undo.RecordObject(target, _title);
    }

    [DrawGizmo(GizmoType.InSelectionHierarchy)]
    private static void DrawHitBox(BoxCollider_Custum _component, GizmoType _gizmoType)
    {
        Gizmos.matrix = _component.transform.localToWorldMatrix;
        Gizmos.color = Color.green;

        Gizmos.DrawWireCube(_component[GUIMode.Pos], _component[GUIMode.Scale] * 2);
    }
}

public enum GUIMode
{
    None,
    Pos,
    Scale
}
#endif