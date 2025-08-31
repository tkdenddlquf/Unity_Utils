using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LocalizeAssetKey))]
public class LocalizeAssetKeyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty dataField = property.FindPropertyRelative("key");

        EditorGUI.PropertyField(position, dataField, label);
    }
}