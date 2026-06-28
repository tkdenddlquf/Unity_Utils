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
                makeItem = () =>
                {
                    PopupField<EntryData> entryPopup = new() { choices = entries };

                    // 콜백은 요소 생성 시 한 번만 등록하고, 현재 행 인덱스는 bindItem에서 갱신한다.
                    entryPopup.RegisterValueChangedCallback(evt =>
                    {
                        int index = (int)entryPopup.userData;

                        entryIDs.GetArrayElementAtIndex(index).longValue = evt.newValue.id;
                        entryKeys.GetArrayElementAtIndex(index).stringValue = evt.newValue.key;

                        property.serializedObject.ApplyModifiedProperties();

                        EditorUtility.SetDirty(property.serializedObject.targetObject);
                    });

                    return entryPopup;
                },
                bindItem = (element, index) =>
                {
                    PopupField<EntryData> entryPopup = (PopupField<EntryData>)element;

                    entryPopup.userData = index;

                    SerializedProperty idProp = entryIDs.GetArrayElementAtIndex(index);
                    SerializedProperty nameProp = entryKeys.GetArrayElementAtIndex(index);

                    entryPopup.SetValueWithoutNotify(new EntryData(idProp.longValue, nameProp.stringValue));
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

            // 테이블 전환 시: 선택 테이블 저장 + 항목 목록(entries) 교체 + 기존 항목 선택 비우기 + 드롭다운 갱신.
            popup.RegisterValueChangedCallback(evt =>
            {
                int index = popup.index;

                if (index < 0 || index >= collections.Count) return;

                LocalizationTableCollection collection = collections[index];

                tableKey.stringValue = collection.TableCollectionName;
                tableGuid.stringValue = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString();

                collection.SetEntries(entries);

                // 이전 테이블 소속 항목은 모두 무효이므로 싹 비운다.
                entryIDs.ClearArray();
                entryKeys.ClearArray();

                property.serializedObject.ApplyModifiedProperties();

                EditorUtility.SetDirty(property.serializedObject.targetObject);

                listView.itemsSource = Enumerable.Range(0, entryKeys.arraySize).ToList();
                listView.Rebuild();
            });

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
    }
}