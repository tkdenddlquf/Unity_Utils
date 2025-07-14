using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.UI;
using UnityEngine;

namespace Yang.Localize
{
    [CustomPropertyDrawer(typeof(LocalizeTableAttribute))]
    public class LocalizeTableAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.LabelField(position, label.text, "Use with reference fields only.");

                return;
            }

            Type fieldType = fieldInfo.FieldType;

            List<LocalizationTableCollection> collections = new();

            if (typeof(StringTableCollection).IsAssignableFrom(fieldType)) collections.AddRange(LocalizationEditorSettings.GetStringTableCollections());
            else if (typeof(AssetTableCollection).IsAssignableFrom(fieldType)) collections.AddRange(LocalizationEditorSettings.GetAssetTableCollections());
            else
            {
                EditorGUI.LabelField(position, label.text, $"Unsupported table type: {fieldType.Name}");

                return;
            }

            int selectedIndex = collections.FindIndex(c => property.objectReferenceValue == c);

            if (selectedIndex < 0) selectedIndex = 0;

            EditorGUI.BeginProperty(position, label, property);

            Rect labelRect = new(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, label);

            float dropdownWidth = position.width - EditorGUIUtility.labelWidth - 25f;

            Rect dropdownRect = new(position.x + EditorGUIUtility.labelWidth, position.y, dropdownWidth, EditorGUIUtility.singleLineHeight);
            Rect openButtonRect = new(dropdownRect.xMax + 5f, position.y, 20f, EditorGUIUtility.singleLineHeight);

            string[] options = new string[collections.Count];

            for (int i = 0; i < collections.Count; i++)
            {
                string group = collections[i].Group;
                string name = collections[i].TableCollectionName;

                options[i] = string.IsNullOrEmpty(group) ? name : $"{group}/{name}";
            }

            int newIndex = EditorGUI.Popup(dropdownRect, selectedIndex, options);

            if (newIndex != selectedIndex)
            {
                property.objectReferenceValue = collections[newIndex];
                property.serializedObject.ApplyModifiedProperties();

                selectedIndex = newIndex;
            }

            if (GUI.Button(openButtonRect, EditorGUIUtility.IconContent("Folder Icon"))) LocalizationTablesWindow.ShowWindow(collections[selectedIndex]);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;
    }
}
