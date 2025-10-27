using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.UI;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Yang.Localize
{
    [CustomPropertyDrawer(typeof(LocalizeData))]
    public class LocalizeDataDrawer : PropertyDrawer
    {
        private ReorderableList entriesList;
        private bool foldout = true;

        private List<GUIContent> contents;
        private List<string> values;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty typeProp = property.FindPropertyRelative("type");

            SerializedProperty tableProp = property.FindPropertyRelative("tableName");
            SerializedProperty entryProp = property.FindPropertyRelative("entryNames");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect typeRect = new(position.x, position.y, position.width, lineHeight);
            Rect tableRect = new(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            EditorGUI.PropertyField(typeRect, typeProp, new GUIContent(typeProp.displayName));

            List<LocalizationTableCollection> collections = GetCollections((LocalizeTableType)typeProp.enumValueIndex);

            int selectedTableIndex = GetTableIndex(collections, tableProp.stringValue);

            EditorGUI.BeginProperty(tableRect, new GUIContent(tableProp.displayName), tableProp);

            Rect labelRect = new(tableRect.x, tableRect.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, tableProp.displayName);

            float dropdownWidth = tableRect.width - EditorGUIUtility.labelWidth - 25f;

            Rect dropdownRect = new(tableRect.x + EditorGUIUtility.labelWidth, tableRect.y, dropdownWidth, EditorGUIUtility.singleLineHeight);
            Rect openButtonRect = new(dropdownRect.xMax + 5f, tableRect.y, 20f, EditorGUIUtility.singleLineHeight);

            string[] tableOptions = GetTableOptios(collections);

            int newTableIndex = EditorGUI.Popup(dropdownRect, selectedTableIndex, tableOptions);

            if (newTableIndex != selectedTableIndex)
            {
                tableProp.stringValue = collections[newTableIndex].TableCollectionName;

                selectedTableIndex = newTableIndex;
            }

            if (GUI.Button(openButtonRect, EditorGUIUtility.IconContent("Folder Icon"))) LocalizationTablesWindow.ShowWindow(collections[selectedTableIndex]);

            contents = GetEntryOptions(selectedTableIndex == -1 ? null : collections[selectedTableIndex], out values);

            Rect foldoutRect = new(position.x, tableRect.y + lineHeight + spacing, position.width, lineHeight);
            Rect entryRect = new(position.x, foldoutRect.y + lineHeight + spacing, position.width, lineHeight);

            if (property.serializedObject.isEditingMultipleObjects)
            {
                EditorGUI.LabelField(foldoutRect, "Multi-object editing is not supported.", EditorStyles.helpBox);
                EditorGUI.EndProperty();

                return;
            }

            foldout = EditorGUI.Foldout(foldoutRect, foldout, entryProp.displayName, true);

            if (foldout)
            {
                entriesList ??= new(property.serializedObject, entryProp, true, false, true, true)
                {
                    elementHeight = lineHeight,

                    drawElementCallback = (rect, index, isActive, isFocused) =>
                    {
                        SerializedProperty prop = entryProp.GetArrayElementAtIndex(index);

                        int selectedEntryIndex = values.IndexOf(prop.stringValue);

                        Rect popupRect = new(rect.x, rect.y, rect.width, lineHeight);

                        selectedEntryIndex = EditorGUI.Popup(popupRect, new($"Element {index}"), selectedEntryIndex, contents.ToArray());

                        if (selectedEntryIndex >= 0 && selectedEntryIndex < values.Count) prop.stringValue = values[selectedEntryIndex];
                    },
                };

                entriesList.DoList(entryRect);
            }

            EditorGUI.EndProperty();
        }

        private List<LocalizationTableCollection> GetCollections(LocalizeTableType type)
        {
            List<LocalizationTableCollection> collections = new();

            switch (type)
            {
                case LocalizeTableType.Asset:
                    foreach (LocalizationTableCollection collection in LocalizationEditorSettings.GetAssetTableCollections())
                    {
                        collections.Add(collection);
                    }
                    break;

                case LocalizeTableType.String:
                    foreach (LocalizationTableCollection collection in LocalizationEditorSettings.GetStringTableCollections())
                    {
                        collections.Add(collection);
                    }
                    break;
            }

            return collections;
        }

        private int GetTableIndex(List<LocalizationTableCollection> collections, string value)
        {
            for (int i = 0; i < collections.Count; i++)
            {
                if (collections[i].TableCollectionName == value) return i;
            }

            return -1;
        }

        private string[] GetTableOptios(List<LocalizationTableCollection> collections)
        {
            string[] options = new string[collections.Count];

            for (int i = 0; i < collections.Count; i++)
            {
                string tableName = collections[i].TableCollectionName;
                string group = collections[i].Group;

                options[i] = string.IsNullOrEmpty(group) ? tableName : $"{group}/{tableName}";
            }

            return options;
        }

        private List<GUIContent> GetEntryOptions(LocalizationTableCollection collection, out List<string> values)
        {
            List<GUIContent> contents = new();

            values = new();

            if (collection != null)
            {
                foreach (SharedTableData.SharedTableEntry key in collection.SharedData.Entries)
                {
                    string tooltip = "";

                    if (collection.GetTable(SystemLanguage.Korean) is StringTable stringTable)
                    {
                        StringTableEntry entry = stringTable.GetEntry(key.Key);

                        if (entry != null) tooltip = entry.Value;
                    }

                    GUIContent content = new(key.Key, tooltip);

                    contents.Add(content);
                    values.Add(key.Key);
                }
            }

            return contents;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3;

            if (foldout && entriesList != null) height += entriesList.GetHeight();

            return height;
        }
    }
}