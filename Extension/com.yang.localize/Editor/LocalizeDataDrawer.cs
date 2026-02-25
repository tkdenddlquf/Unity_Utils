using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.UI;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Yang.Localize.Editor
{
    [CustomPropertyDrawer(typeof(LocalizeData))]
    public class LocalizeDataDrawer : PropertyDrawer
    {
        private ReorderableList entriesList;
        private bool foldout = true;

        private string[] tables;

        private EntryData[] entries;
        private GUIContent[] contents;

        private IReadOnlyList<LocalizationTableCollection> collections;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty typeProp = property.FindPropertyRelative("type");

            SerializedProperty tableName = property.FindPropertyRelative("tableName");
            SerializedProperty tableGuid = property.FindPropertyRelative("tableGuid");

            SerializedProperty entryNames = property.FindPropertyRelative("entryNames");
            SerializedProperty entryIDs = property.FindPropertyRelative("entryIDs");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect typeRect = new(position.x, position.y, position.width, lineHeight);
            Rect tableRect = new(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            EditorGUI.PropertyField(typeRect, typeProp, new GUIContent(typeProp.displayName));

            SetTables((LocalizeTableType)typeProp.enumValueIndex);

            EditorGUI.BeginProperty(tableRect, new GUIContent(tableName.displayName), tableName);

            Rect labelRect = new(tableRect.x, tableRect.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, tableName.displayName);

            float dropdownWidth = tableRect.width - EditorGUIUtility.labelWidth - 25f;

            Rect dropdownRect = new(tableRect.x + EditorGUIUtility.labelWidth, tableRect.y, dropdownWidth, EditorGUIUtility.singleLineHeight);
            Rect openButtonRect = new(dropdownRect.xMax + 5f, tableRect.y, 20f, EditorGUIUtility.singleLineHeight);

            int tableIndex = EditorGUI.Popup(dropdownRect, GetTableIndex(tableName.stringValue, tableGuid.stringValue), tables);

            LocalizationTableCollection collection;

            if (tableIndex >= 0 && tableIndex < collections.Count)
            {
                collection = collections[tableIndex];

                tableName.stringValue = collection.TableCollectionName;
                tableGuid.stringValue = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString();
            }
            else collection = null;

            if (GUI.Button(openButtonRect, EditorGUIUtility.IconContent("Folder Icon")) && collection != null) LocalizationTablesWindow.ShowWindow(collection);

            SetEntries(collection);

            Rect foldoutRect = new(position.x, tableRect.y + lineHeight + spacing, position.width, lineHeight);
            Rect entryRect = new(position.x, foldoutRect.y + lineHeight + spacing, position.width, lineHeight);

            if (property.serializedObject.isEditingMultipleObjects)
            {
                EditorGUI.LabelField(foldoutRect, "Multi-object editing is not supported.", EditorStyles.helpBox);
                EditorGUI.EndProperty();

                return;
            }

            foldout = EditorGUI.Foldout(foldoutRect, foldout, entryNames.displayName, true);

            if (foldout)
            {
                entriesList ??= new(property.serializedObject, entryNames, true, false, true, true)
                {
                    elementHeight = lineHeight,

                    drawElementCallback = (rect, index, isActive, isFocused) =>
                    {
                        SerializedProperty entryName = entryNames.GetArrayElementAtIndex(index);

                        if (entryIDs.arraySize != entryNames.arraySize) entryIDs.arraySize = entryNames.arraySize;

                        SerializedProperty entryID = entryIDs.GetArrayElementAtIndex(index);

                        int entryIndex = System.Array.IndexOf(entries, new EntryData(entryID.longValue, entryName.stringValue, ""));

                        Rect popupRect = new(rect.x, rect.y, rect.width, lineHeight);

                        entryIndex = EditorGUI.Popup(popupRect, new($"Element {index}"), entryIndex, contents);

                        if (entryIndex >= 0 && entryIndex < entries.Length)
                        {
                            entryID.longValue = entries[entryIndex].id;
                            entryName.stringValue = entries[entryIndex].key;
                        }
                    },
                };

                entriesList.DoList(entryRect);
            }

            EditorGUI.EndProperty();
        }

        private void SetTables(LocalizeTableType type)
        {
            collections = type switch
            {
                LocalizeTableType.Asset => LocalizationEditorSettings.GetAssetTableCollections(),
                LocalizeTableType.String => LocalizationEditorSettings.GetStringTableCollections(),
                _ => null,
            };

            if (collections != null)
            {
                if (tables == null || tables.Length != collections.Count) tables = new string[collections.Count];

                for (int i = 0; i < collections.Count; i++)
                {
                    LocalizationTableCollection collection = collections[i];

                    string tableName = collection.TableCollectionName;
                    string group = collection.Group;

                    tables[i] = string.IsNullOrEmpty(group) ? tableName : $"{group}/{tableName}";
                }
            }
        }

        private int GetTableIndex(string value, string stringGuid)
        {
            System.Guid.TryParse(stringGuid, out System.Guid guid);

            for (int i = 0; i < collections.Count; i++)
            {
                LocalizationTableCollection collection = collections[i];

                if (collection.TableCollectionName == value || collection.TableCollectionNameReference.TableCollectionNameGuid == guid) return i;
            }

            return -1;
        }

        private void SetEntries(LocalizationTableCollection collection)
        {
            if (collection != null)
            {
                List<SharedTableData.SharedTableEntry> datas = collection.SharedData.Entries;

                if (entries == null || entries.Length != datas.Count)
                {
                    entries = new EntryData[datas.Count];
                    contents = new GUIContent[datas.Count];
                }

                for (int i = 0; i < datas.Count; i++)
                {
                    SharedTableData.SharedTableEntry current = datas[i];
                    string tooltip = "";

                    if (collection.GetTable(Application.systemLanguage) is StringTable stringTable)
                    {
                        StringTableEntry entry = stringTable.GetEntry(current.Key);

                        if (entry != null) tooltip = entry.Value;
                    }

                    entries[i] = new(current.Id, current.Key, tooltip);
                    contents[i] = new GUIContent(current.Key, tooltip);
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