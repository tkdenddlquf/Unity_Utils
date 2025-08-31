using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.UI;
using UnityEngine;
using Yang.Localize;

namespace Yang.Localize
{
    [CustomPropertyDrawer(typeof(LocalizeTable))]
    public class LocalizeTableDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty tableProp = property.FindPropertyRelative("tableName");
            SerializedProperty typeProp = property.FindPropertyRelative("type");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect typeRect = new(position.x, position.y, position.width, lineHeight);
            Rect tableRect = new(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            EditorGUI.PropertyField(typeRect, typeProp, new GUIContent(typeProp.displayName));

            List<string> collections = new();
            List<string> groups = new();

            switch ((LocalizeTableType)typeProp.enumValueIndex)
            {
                case LocalizeTableType.Asset:
                    foreach (LocalizationTableCollection collection in LocalizationEditorSettings.GetAssetTableCollections())
                    {
                        collections.Add(collection.TableCollectionName);
                        groups.Add(collection.Group);
                    }
                    break;

                case LocalizeTableType.String:
                    foreach (LocalizationTableCollection collection in LocalizationEditorSettings.GetStringTableCollections())
                    {
                        collections.Add(collection.TableCollectionName);
                        groups.Add(collection.Group);
                    }
                    break;
            }

            int selectedIndex = collections.IndexOf(tableProp.stringValue);

            EditorGUI.BeginProperty(tableRect, new GUIContent(tableProp.displayName), tableProp);

            Rect labelRect = new(tableRect.x, tableRect.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, label);

            float dropdownWidth = tableRect.width - EditorGUIUtility.labelWidth - 25f;

            Rect dropdownRect = new(tableRect.x + EditorGUIUtility.labelWidth, tableRect.y, dropdownWidth, EditorGUIUtility.singleLineHeight);
            Rect openButtonRect = new(dropdownRect.xMax + 5f, tableRect.y, 20f, EditorGUIUtility.singleLineHeight);

            string[] options = new string[collections.Count];

            for (int i = 0; i < collections.Count; i++) options[i] = string.IsNullOrEmpty(groups[i]) ? collections[i] : $"{groups[i]}/{collections[i]}";

            int newIndex = EditorGUI.Popup(dropdownRect, selectedIndex, options);

            if (newIndex != selectedIndex)
            {
                tableProp.stringValue = collections[newIndex];
                tableProp.serializedObject.ApplyModifiedProperties();

                selectedIndex = newIndex;
            }

            if (GUI.Button(openButtonRect, EditorGUIUtility.IconContent("Folder Icon"))) LocalizationTablesWindow.ShowWindow(collections[selectedIndex], "");

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2;
        }
    }
}