using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.UI;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Yang.Localize.Editor
{
    [CustomPropertyDrawer(typeof(LocalizeData))]
    public class LocalizeDataDrawer : PropertyDrawer
    {
        private IReadOnlyList<LocalizationTableCollection> collections;

        private readonly List<string> tables = new();
        private readonly List<EntryData> entries = new();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = new();

            SerializedProperty typeProp = property.FindPropertyRelative("type");

            SerializedProperty tableKey = property.FindPropertyRelative("tableKey");
            SerializedProperty tableGuid = property.FindPropertyRelative("tableGuid");

            SerializedProperty entryIDs = property.FindPropertyRelative("entryIDs");
            SerializedProperty entryKeys = property.FindPropertyRelative("entryKeys");

            PropertyField typeField = new(typeProp);

            root.Add(typeField);

            if (!SetTables((LocalizeTableType)typeProp.enumValueIndex)) return root;

            VisualElement tableRow = new();

            tableRow.style.flexDirection = FlexDirection.Row;
            tableRow.style.alignItems = Align.Center;

            int tableIndex = collections.GetTableIndex(tableKey.stringValue, tableGuid.stringValue);

            PopupField<string> popup = new(tables, tableIndex)
            {
                label = tableKey.displayName
            };

            popup.style.flexGrow = 1;
            popup.style.flexShrink = 1;

            if (tableIndex != -1) collections[tableIndex].SetEntries(entries);

            Button openButton = new(() =>
            {
                int index = popup.index;

                if (index >= 0 && index < collections.Count) LocalizationTablesWindow.ShowWindow(collections[index]);
            });

            openButton.style.width = 20;
            // openButton.style.backgroundImage = EditorGUIUtility.IconContent("Folder Icon");

            popup.RegisterValueChangedCallback(evt =>
            {
                int index = popup.index;

                if (index >= 0 && index < collections.Count)
                {
                    LocalizationTableCollection collection = collections[index];

                    tableKey.stringValue = collection.TableCollectionName;
                    tableGuid.stringValue = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString();

                    collection.SetEntries(entries);

                    property.serializedObject.ApplyModifiedProperties();

                    EditorUtility.SetDirty(property.serializedObject.targetObject);
                }
            });

            tableRow.Add(popup);
            tableRow.Add(openButton);

            root.Add(tableRow);

            if (property.serializedObject.isEditingMultipleObjects)
            {
                root.Add(new HelpBox("Multi-object editing is not supported.", HelpBoxMessageType.Info));

                return root;
            }

            ListView listView = new()
            {
                headerTitle = "Items",
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                showFoldoutHeader = true,
                showAddRemoveFooter = true,
                itemsSource = Enumerable.Range(0, entryKeys.arraySize).ToList(),
                makeItem = () => new PopupField<EntryData>(entries, 0),
                bindItem = (element, index) =>
                {
                    PopupField<EntryData> popup = (PopupField<EntryData>)element;

                    SerializedProperty idProp = entryIDs.GetArrayElementAtIndex(index);
                    SerializedProperty nameProp = entryKeys.GetArrayElementAtIndex(index);

                    EntryData entryData = new(idProp.longValue, nameProp.stringValue);

                    popup.SetValueWithoutNotify(entryData);
                    popup.RegisterValueChangedCallback(evt =>
                    {
                        EntryData newValue = evt.newValue;

                        PopupField<EntryData> target = FindParentInCurrent<PopupField<EntryData>>(evt.target);

                        int targetIndex = target.parent.IndexOf(target);

                        SerializedProperty idProp = entryIDs.GetArrayElementAtIndex(targetIndex);
                        SerializedProperty nameProp = entryKeys.GetArrayElementAtIndex(targetIndex);

                        idProp.longValue = newValue.id;
                        nameProp.stringValue = newValue.key;

                        property.serializedObject.ApplyModifiedProperties();

                        EditorUtility.SetDirty(property.serializedObject.targetObject);
                    });
                }
            };

            listView.itemsAdded += indexes =>
            {
                foreach (int index in indexes)
                {
                    entryIDs.InsertArrayElementAtIndex(entryIDs.arraySize);
                    entryKeys.InsertArrayElementAtIndex(entryKeys.arraySize);
                }

                entryIDs.serializedObject.ApplyModifiedProperties();
                entryKeys.serializedObject.ApplyModifiedProperties();
            };

            listView.itemsRemoved += indexes =>
            {
                foreach (int index in indexes.OrderByDescending(x => x))
                {
                    entryIDs.DeleteArrayElementAtIndex(index);
                    entryKeys.DeleteArrayElementAtIndex(index);
                }

                entryIDs.serializedObject.ApplyModifiedProperties();
                entryKeys.serializedObject.ApplyModifiedProperties();
            };

            listView.itemIndexChanged += (from, to) =>
            {
                entryIDs.MoveArrayElement(from, to);
                entryKeys.MoveArrayElement(from, to);

                entryIDs.serializedObject.ApplyModifiedProperties();
                entryKeys.serializedObject.ApplyModifiedProperties();
            };

            root.Add(listView);

            return root;
        }

        private bool SetTables(LocalizeTableType type)
        {
            collections = type switch
            {
                LocalizeTableType.Asset => LocalizationEditorSettings.GetAssetTableCollections(),
                LocalizeTableType.String => LocalizationEditorSettings.GetStringTableCollections(),
                _ => null,
            };

            if (collections == null) return false;

            collections.SetTables(tables);

            return true;
        }

        private T FindParentInCurrent<T>(IEventHandler handler) where T : VisualElement
        {
            VisualElement current = handler as VisualElement;

            while (current != null)
            {
                if (current is T target) return target;

                current = current.parent;
            }

            return null;
        }
    }
}