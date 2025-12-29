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

        private readonly List<GUIContent> contents = new();
        private readonly List<LocalizationTableCollection> collections = new();

        private readonly List<long> ids = new();
        private readonly List<string> values = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty typeProp = property.FindPropertyRelative("type");

            SerializedProperty tableProp = property.FindPropertyRelative("tableName");
            SerializedProperty entriesProp = property.FindPropertyRelative("entryNames");

            SerializedProperty guidProp = property.FindPropertyRelative("tableGuid");
            SerializedProperty idsProp = property.FindPropertyRelative("entryIDs");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect typeRect = new(position.x, position.y, position.width, lineHeight);
            Rect tableRect = new(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            EditorGUI.PropertyField(typeRect, typeProp, new GUIContent(typeProp.displayName));

            SetCollections((LocalizeTableType)typeProp.enumValueIndex);

            int selectedTableIndex = GetTableIndex(tableProp.stringValue);

            if (selectedTableIndex == -1 && guidProp.stringValue != "") selectedTableIndex = GetTableIndex(System.Guid.Parse(guidProp.stringValue));

            EditorGUI.BeginProperty(tableRect, new GUIContent(tableProp.displayName), tableProp);

            Rect labelRect = new(tableRect.x, tableRect.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, tableProp.displayName);

            float dropdownWidth = tableRect.width - EditorGUIUtility.labelWidth - 25f;

            Rect dropdownRect = new(tableRect.x + EditorGUIUtility.labelWidth, tableRect.y, dropdownWidth, EditorGUIUtility.singleLineHeight);
            Rect openButtonRect = new(dropdownRect.xMax + 5f, tableRect.y, 20f, EditorGUIUtility.singleLineHeight);

            string[] tableOptions = GetTableOptios();

            int newTableIndex = EditorGUI.Popup(dropdownRect, selectedTableIndex, tableOptions);

            if (newTableIndex != selectedTableIndex)
            {
                LocalizationTableCollection collection = collections[newTableIndex];

                tableProp.stringValue = collection.TableCollectionName;
                guidProp.stringValue = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString();

                selectedTableIndex = newTableIndex;
            }

            if (guidProp.stringValue == "" && tableProp.stringValue != "") guidProp.stringValue = collections[newTableIndex].TableCollectionNameReference.TableCollectionNameGuid.ToString();

            if (GUI.Button(openButtonRect, EditorGUIUtility.IconContent("Folder Icon"))) LocalizationTablesWindow.ShowWindow(collections[selectedTableIndex]);

            SetEntryOptions(selectedTableIndex == -1 ? null : collections[selectedTableIndex]);

            Rect foldoutRect = new(position.x, tableRect.y + lineHeight + spacing, position.width, lineHeight);
            Rect entryRect = new(position.x, foldoutRect.y + lineHeight + spacing, position.width, lineHeight);

            if (property.serializedObject.isEditingMultipleObjects)
            {
                EditorGUI.LabelField(foldoutRect, "Multi-object editing is not supported.", EditorStyles.helpBox);
                EditorGUI.EndProperty();

                return;
            }

            foldout = EditorGUI.Foldout(foldoutRect, foldout, entriesProp.displayName, true);

            if (foldout)
            {
                entriesList ??= new(property.serializedObject, entriesProp, true, false, true, true)
                {
                    elementHeight = lineHeight,

                    drawElementCallback = (rect, index, isActive, isFocused) =>
                    {
                        SerializedProperty entryprop = entriesProp.GetArrayElementAtIndex(index);

                        if (idsProp.arraySize != entriesProp.arraySize) idsProp.arraySize = entriesProp.arraySize;

                        SerializedProperty idprop = idsProp.GetArrayElementAtIndex(index);

                        int selectedIndex = values.IndexOf(entryprop.stringValue);

                        if (selectedIndex == -1) selectedIndex = ids.IndexOf(idprop.longValue);

                        Rect popupRect = new(rect.x, rect.y, rect.width, lineHeight);

                        selectedIndex = EditorGUI.Popup(popupRect, new($"Element {index}"), selectedIndex, contents.ToArray());

                        if (selectedIndex >= 0 && selectedIndex < values.Count)
                        {
                            entryprop.stringValue = values[selectedIndex];
                            idprop.longValue = ids[selectedIndex];
                        }
                    },
                };

                entriesList.DoList(entryRect);
            }

            EditorGUI.EndProperty();
        }

        private void SetCollections(LocalizeTableType type)
        {
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
        }

        private int GetTableIndex(string value)
        {
            for (int i = 0; i < collections.Count; i++)
            {
                if (collections[i].TableCollectionName == value) return i;
            }

            return -1;
        }

        private int GetTableIndex(System.Guid value)
        {
            for (int i = 0; i < collections.Count; i++)
            {
                if (collections[i].TableCollectionNameReference.TableCollectionNameGuid == value) return i;
            }

            return -1;
        }

        private string[] GetTableOptios()
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

        private void SetEntryOptions(LocalizationTableCollection collection)
        {
            contents.Clear();

            ids.Clear();
            values.Clear();

            if (collection != null)
            {
                List<SharedTableData.SharedTableEntry> entries = collection.SharedData.Entries;

                for (int i = 0; i < entries.Count; i++)
                {
                    string tooltip = "";
                    SharedTableData.SharedTableEntry current = entries[i];

                    if (collection.GetTable(SystemLanguage.Korean) is StringTable stringTable)
                    {
                        StringTableEntry entry = stringTable.GetEntry(current.Key);

                        if (entry != null) tooltip = entry.Value;
                    }

                    GUIContent content = new(current.Key, tooltip);

                    contents.Add(content);

                    ids.Add(current.Id);
                    values.Add(current.Key);
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3;

            if (foldout && entriesList != null) height += entriesList.GetHeight();

            return height;
        }
    }
}