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
        private LocalizationTableCollection table;

        private LocalizeAttribute TargetAttribute => (LocalizeAttribute)attribute;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, "ERROR", "Invalid attribute parameter");

                return;
            }

            SerializedProperty dataField = FindBaseOrSiblingProperty(property, TargetAttribute.DataField);

            if (dataField != null)
            {
                table = dataField.objectReferenceValue as LocalizationTableCollection;

                if (dataField.objectReferenceValue == null || table == null)
                {
                    EditorGUI.LabelField(position, "ERROR", "None table");

                    return;
                }
            }

            position = EditorGUI.PrefixLabel(position, label);

            List<GUIContent> contents = new();
            List<string> values = new();

            SetContents(contents, values, table.SharedData);

            int currentIndex = values.IndexOf(property.stringValue);
            int previousIndex = currentIndex;

            currentIndex = EditorGUI.Popup(position, currentIndex, contents.ToArray());

            if (previousIndex != currentIndex)
            {
                property.stringValue = values[currentIndex];
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        private void SetContents(List<GUIContent> contents, List<string> values, SharedTableData data)
        {
            foreach (SharedTableData.SharedTableEntry key in data.Entries)
            {
                StringTableCollection tableCollection = LocalizationEditorSettings.GetStringTableCollection(data.TableCollectionName);

                foreach (StringTable table in tableCollection.StringTables)
                {
                    if (table.LocaleIdentifier != SystemLanguage.Korean) continue;

                    StringTableEntry tableEntry = table.GetEntry(key.Key);
                    GUIContent content = new(key.Key, tableEntry == null ? "" : tableEntry.Value);

                    contents.Add(content);
                    values.Add(key.Key);

                    break;
                }

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

                        newPropertyPath = propertyPath.Remove(dotIndex + 1) + propertyName;
                        relativeProperty = property.serializedObject.FindProperty(newPropertyPath);
                    }
                }
            }

            return relativeProperty;
        }
    }
}