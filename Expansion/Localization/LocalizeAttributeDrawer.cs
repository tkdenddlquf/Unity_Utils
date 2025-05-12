using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

[CustomPropertyDrawer(typeof(LocalizeAttribute))]
public class LocalizeAttributeDrawer : PropertyDrawer
{
    private StringTableCollection table;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, "ERROR", "Invalid attribute parameter");
            return;
        }

        SerializedProperty dataField = property.serializedObject.FindProperty(((LocalizeAttribute)attribute).dataField);

        if (dataField != null)
        {
            Object objectReference = dataField.objectReferenceValue;

            if (objectReference is StringTableCollection table) this.table = table;
            else if (objectReference != null)
            {
                EditorGUI.LabelField(position, "ERROR", "Invalid data type");
                return;
            }
        }
        else
        {
            EditorGUI.LabelField(position, "ERROR", "Invalid data");
            return;
        }

        if (table == null)
        {
            EditorGUI.LabelField(position, "ERROR", "None table");

            table = property.serializedObject.targetObject as StringTableCollection;

            if (table == null) return;
        }

        position = EditorGUI.PrefixLabel(position, label);

        List<SharedTableData.SharedTableEntry> keys = table.SharedData.Entries;

        List<GUIContent> contents = new();
        List<string> values = new();

        SetContents(contents, values, keys);

        int currentIndex = values.IndexOf(property.stringValue);
        int previousIndex = currentIndex;

        currentIndex = EditorGUI.Popup(position, currentIndex, contents.ToArray());

        if (previousIndex != currentIndex)
        {
            property.stringValue = values[currentIndex];
            property.serializedObject.ApplyModifiedProperties();
        }
    }

    private void SetContents(List<GUIContent> contents, List<string> values, List<SharedTableData.SharedTableEntry> keys)
    {
        foreach (SharedTableData.SharedTableEntry key in keys)
        {
            contents.Add(new GUIContent(key.Key));
            values.Add(key.Key);
        }
    }
}