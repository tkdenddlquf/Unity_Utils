using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Yang.Localize
{
    [CustomPropertyDrawer(typeof(LocalizeAttribute))]
    public class LocalizeAttributeDrawer : PropertyDrawer
    {
        private LocalizeAttribute TargetAttribute => (LocalizeAttribute)attribute;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, "ERROR", "Invalid attribute parameter");

                return;
            }

            SerializedProperty dataField = FindBaseOrSiblingProperty(property, TargetAttribute.DataField);

            if (dataField == null)
            {
                EditorGUI.LabelField(position, "ERROR", "None table");

                return;
            }

            position = EditorGUI.PrefixLabel(position, label);

            List<GUIContent> contents = new();
            List<string> values = new();

            string tableName = dataField.FindPropertyRelative("tableName").stringValue;
            SerializedProperty typeProp = dataField.FindPropertyRelative("type");

            LocalizationTableCollection tableCollection = null;

            switch ((LocalizeTableType)typeProp.enumValueIndex)
            {
                case LocalizeTableType.Asset:
                    tableCollection = LocalizationEditorSettings.GetAssetTableCollection(tableName);
                    break;

                case LocalizeTableType.String:
                    tableCollection = LocalizationEditorSettings.GetStringTableCollection(tableName);
                    break;
            }

            if (tableCollection == null) return;

            SetContents(contents, values, tableCollection);

            int currentIndex = values.IndexOf(property.stringValue);
            int previousIndex = currentIndex;

            currentIndex = EditorGUI.Popup(position, currentIndex, contents.ToArray());

            if (previousIndex != currentIndex)
            {
                property.stringValue = values[currentIndex];
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        private void SetContents(List<GUIContent> contents, List<string> values, LocalizationTableCollection tableCollection)
        {
            foreach (SharedTableData.SharedTableEntry key in tableCollection.SharedData.Entries)
            {
                LocalizationTable table = tableCollection.GetTable(SystemLanguage.Korean);

                string tooltip = "";

                if (table is StringTable stringTable)
                {
                    StringTableEntry entry = stringTable.GetEntry(key.Key);

                    if (entry != null) tooltip = entry.Value;
                }

                GUIContent content = new(key.Key, tooltip);

                contents.Add(content);
                values.Add(key.Key);

            }
        }

        private SerializedProperty FindBaseOrSiblingProperty(SerializedProperty property, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return null;

            SerializedProperty relativeProperty = property.serializedObject.FindProperty(propertyName);

            if (relativeProperty == null)
            {
                string propertyPath = property.propertyPath;
                int localPathLength = property.name.Length;

                string newPropertyPath = propertyPath[..^localPathLength] + propertyName;

                relativeProperty = property.serializedObject.FindProperty(newPropertyPath);

                if (relativeProperty == null && property.isArray)
                {
                    int propertyPathLength = propertyPath.Length;

                    int dotCount = 0;
                    const int SiblingOfListDotCount = 3;

                    for (int i = 1; i < propertyPathLength; i++)
                    {
                        if (propertyPath[propertyPathLength - i] == '.')
                        {
                            dotCount++;

                            if (dotCount >= SiblingOfListDotCount)
                            {
                                localPathLength = i - 1;

                                break;
                            }
                        }
                    }

                    newPropertyPath = propertyPath[..^localPathLength] + propertyName;
                    relativeProperty = property.serializedObject.FindProperty(newPropertyPath);
                }

                if (relativeProperty == null)
                {
                    int dotIndex = propertyPath.Length - property.name.Length - 1;

                    while (relativeProperty == null)
                    {
                        dotIndex = propertyPath.LastIndexOf('.', dotIndex - 1);

                        if (dotIndex < 0) break;

                        newPropertyPath = propertyPath[..(dotIndex + 1)] + propertyName;
                        relativeProperty = property.serializedObject.FindProperty(newPropertyPath);
                    }
                }
            }

            return relativeProperty;
        }
    }
}