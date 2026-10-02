using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Localization;
using UnityEditor.Localization.UI;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Localize.Editor
{
    [CustomPropertyDrawer(typeof(LocalizeData))]
    public class LocalizeDataDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => new DrawerContext(property).Build();

        private sealed class DrawerContext
        {
            private const float RowHeight = 42f;
            private readonly SerializedProperty property;
            private readonly SerializedProperty typeProp;
            private readonly SerializedProperty tableKeyProp;
            private readonly SerializedProperty tableGuidProp;
            private readonly SerializedProperty entryIDsProp;
            private readonly SerializedProperty entryKeysProp;
            private readonly List<string> tableNames = new();
            private readonly List<EntryData> entries = new();
            private readonly List<int> rowIndices = new();
            private IReadOnlyList<LocalizationTableCollection> collections = Array.Empty<LocalizationTableCollection>();
            private EnumField typeField;
            private DropdownField tableField;
            private Button openButton;
            private Button addButton;
            private ListView listView;
            private Label countLabel;
            private Label emptyLabel;
            private HelpBox invalidTableHelp;
            private HelpBox duplicateHelp;
            private int collectionIndex = -1;

            public DrawerContext(SerializedProperty property)
            {
                this.property = property;
                typeProp = property.FindPropertyRelative("type");
                tableKeyProp = property.FindPropertyRelative("tableKey");
                tableGuidProp = property.FindPropertyRelative("tableGuid");
                entryIDsProp = property.FindPropertyRelative("entryIDs");
                entryKeysProp = property.FindPropertyRelative("entryKeys");
            }

            public VisualElement Build()
            {
                VisualElement root = new();

                if (property.serializedObject.isEditingMultipleObjects)
                {
                    root.Add(new HelpBox("Multi-object editing is not supported.", HelpBoxMessageType.Info));
                    return root;
                }

                NormalizeEntryArrays();
                VisualElement panel = CreatePanel();
                panel.Add(CreateTitle());
                panel.Add(CreateTypeField());
                panel.Add(CreateTableField());
                invalidTableHelp = new HelpBox("The saved localization table could not be found.", HelpBoxMessageType.Warning);
                panel.Add(invalidTableHelp);
                panel.Add(CreateEntriesSection());
                root.Add(panel);
                root.TrackSerializedObjectValue(property.serializedObject, _ => RefreshFromSerializedObject());

                RefreshCollections(false);
                return root;
            }

            private VisualElement CreatePanel()
            {
                VisualElement panel = new();
                Color border = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.14f);

                panel.style.marginTop = 2;
                panel.style.marginBottom = 4;
                panel.style.paddingLeft = 8;
                panel.style.paddingRight = 8;
                panel.style.paddingTop = 7;
                panel.style.paddingBottom = 8;
                panel.style.borderLeftWidth = 1;
                panel.style.borderRightWidth = 1;
                panel.style.borderTopWidth = 1;
                panel.style.borderBottomWidth = 1;
                panel.style.borderLeftColor = border;
                panel.style.borderRightColor = border;
                panel.style.borderTopColor = border;
                panel.style.borderBottomColor = border;
                panel.style.borderTopLeftRadius = 4;
                panel.style.borderTopRightRadius = 4;
                panel.style.borderBottomLeftRadius = 4;
                panel.style.borderBottomRightRadius = 4;
                panel.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.025f) : new Color(0f, 0f, 0f, 0.025f);

                return panel;
            }

            private VisualElement CreateTitle()
            {
                Label title = new(string.IsNullOrEmpty(property.displayName) ? "Localization" : property.displayName);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.marginBottom = 6;
                return title;
            }

            private VisualElement CreateTypeField()
            {
                typeField = new EnumField("Type", (LocalizeTableType)typeProp.enumValueIndex);
                SetFieldLabelWidth(typeField, 52);
                typeField.style.marginBottom = 3;
                typeField.RegisterValueChangedCallback(evt => ChangeType((LocalizeTableType)evt.newValue));
                return typeField;
            }

            private VisualElement CreateTableField()
            {
                VisualElement row = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
                tableField = new DropdownField("Table") { style = { flexGrow = 1, flexShrink = 1 } };
                SetFieldLabelWidth(tableField, 52);
                tableField.RegisterValueChangedCallback(_ => ChangeTable(tableField.index));
                openButton = new(OpenSelectedTable) { text = "↗", tooltip = "Open the selected localization table." };
                openButton.style.width = 24;
                openButton.style.height = 20;
                openButton.style.marginLeft = 4;
                row.Add(tableField);
                row.Add(openButton);
                return row;
            }

            private VisualElement CreateEntriesSection()
            {
                VisualElement section = new();
                VisualElement header = new() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2, marginBottom = 4 } };
                Label title = new("Entries") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } };
                countLabel = new("0") { tooltip = "Selected entry count" };
                countLabel.style.minWidth = 24;
                countLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                countLabel.style.opacity = 0.65f;
                addButton = new(OpenAddPicker) { text = "+ Add Entry", tooltip = "Add a localization entry." };
                addButton.style.height = 20;
                addButton.style.marginLeft = 4;
                header.Add(title);
                header.Add(countLabel);
                header.Add(addButton);
                section.Add(header);
                duplicateHelp = new HelpBox("Duplicate localization entries are selected.", HelpBoxMessageType.Warning);
                duplicateHelp.style.marginBottom = 4;
                section.Add(duplicateHelp);
                emptyLabel = new("No entries selected.");
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.opacity = 0.6f;
                emptyLabel.style.height = 32;
                section.Add(emptyLabel);
                listView = new ListView
                {
                    reorderable = true,
                    reorderMode = ListViewReorderMode.Animated,
                    selectionType = SelectionType.None,
                    fixedItemHeight = RowHeight,
                    makeItem = CreateEntryRow,
                    bindItem = BindEntryRow,
                };
                listView.style.minHeight = RowHeight;
                listView.style.maxHeight = 250;
                listView.itemIndexChanged += MoveEntry;
                section.Add(listView);
                return section;
            }

            private VisualElement CreateEntryRow()
            {
                EntryRow row = new();
                row.pickButton.clicked += () => OpenReplacePicker(row.index, row.pickButton);
                row.removeButton.clicked += () => RemoveEntry(row.index);
                return row;
            }

            private void BindEntryRow(VisualElement element, int index)
            {
                EntryRow row = (EntryRow)element;
                row.index = index;
                row.indexLabel.text = (index + 1).ToString();

                if (index < 0 || index >= entryKeysProp.arraySize) return;
                long id = entryIDsProp.GetArrayElementAtIndex(index).longValue;
                string key = entryKeysProp.GetArrayElementAtIndex(index).stringValue;
                int foundIndex = entries.FindIndex(entry => entry.id == id);
                if (foundIndex >= 0)
                {
                    EntryData entry = entries[foundIndex];
                    row.pickButton.text = entry.key;
                    row.pickButton.tooltip = entry.GetDisplayName(160);
                    row.previewLabel.text = entry.GetPreview();
                    row.previewLabel.tooltip = entry.preview;
                    row.previewLabel.style.display = string.IsNullOrEmpty(entry.preview) ? DisplayStyle.None : DisplayStyle.Flex;
                }
                else
                {
                    row.pickButton.text = string.IsNullOrEmpty(key) ? "Select entry..." : $"{key}  (Missing)";
                    row.pickButton.tooltip = string.IsNullOrEmpty(key) ? "Select a localization entry." : "This entry does not exist in the selected table.";
                    row.previewLabel.text = string.IsNullOrEmpty(key) ? "" : "Entry not found in the selected table.";
                    row.previewLabel.style.display = string.IsNullOrEmpty(key) ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }

            private void RefreshFromSerializedObject()
            {
                typeField.SetValueWithoutNotify((LocalizeTableType)typeProp.enumValueIndex);
                NormalizeEntryArrays();
                RefreshCollections(false);
            }

            private void ChangeType(LocalizeTableType type)
            {
                if (typeProp.enumValueIndex == (int)type) return;

                property.serializedObject.Update();
                typeProp.enumValueIndex = (int)type;
                ClearSelection();
                Apply();
                RefreshCollections(false);
            }

            private void ChangeTable(int index)
            {
                if (index < 0 || index >= collections.Count) return;

                LocalizationTableCollection collection = collections[index];
                string guid = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString();

                if (tableGuidProp.stringValue == guid) return;

                property.serializedObject.Update();
                tableKeyProp.stringValue = collection.TableCollectionName;
                tableGuidProp.stringValue = guid;
                entryIDsProp.ClearArray();
                entryKeysProp.ClearArray();
                Apply();

                collectionIndex = index;
                collection.SetEntries(entries, Application.systemLanguage);
                RefreshEntryUI();
                RefreshTableState();
            }

            private void RefreshCollections(bool clearSelection)
            {
                property.serializedObject.Update();

                LocalizeTableType type = (LocalizeTableType)typeProp.enumValueIndex;
                collections = type switch
                {
                    LocalizeTableType.Asset => LocalizationEditorSettings.GetAssetTableCollections(),
                    LocalizeTableType.String => LocalizationEditorSettings.GetStringTableCollections(),
                    _ => Array.Empty<LocalizationTableCollection>(),
                };

                collections.SetTables(tableNames);
                tableField.choices = new List<string>(tableNames);

                if (clearSelection)
                {
                    ClearSelection();
                    Apply();
                }

                collectionIndex = collections.GetTableIndex(tableKeyProp.stringValue, tableGuidProp.stringValue);
                if (collectionIndex >= 0)
                {
                    tableField.SetValueWithoutNotify(tableNames[collectionIndex]);
                    RepairTableReference();
                    collections[collectionIndex].SetEntries(entries, Application.systemLanguage);
                    RepairEntryReferences();
                }
                else
                {
                    tableField.SetValueWithoutNotify("");
                    entries.Clear();
                }

                RefreshTableState();
                RefreshEntryUI();
            }

            private void RepairTableReference()
            {
                LocalizationTableCollection collection = collections[collectionIndex];
                string tableName = collection.TableCollectionName;
                string tableGuid = collection.TableCollectionNameReference.TableCollectionNameGuid.ToString();
                if (tableKeyProp.stringValue == tableName && tableGuidProp.stringValue == tableGuid) return;

                tableKeyProp.stringValue = tableName;
                tableGuidProp.stringValue = tableGuid;
                Apply();
            }

            private void RepairEntryReferences()
            {
                bool changed = false;
                for (int i = 0; i < entryKeysProp.arraySize; i++)
                {
                    SerializedProperty idProp = entryIDsProp.GetArrayElementAtIndex(i);
                    SerializedProperty keyProp = entryKeysProp.GetArrayElementAtIndex(i);
                    EntryData entry = entries.FirstOrDefault(item => item.id == idProp.longValue);
                    if (entry.id != SharedTableData.EmptyId)
                    {
                        if (keyProp.stringValue != entry.key)
                        {
                            keyProp.stringValue = entry.key;
                            changed = true;
                        }

                        continue;
                    }
                    if (string.IsNullOrEmpty(keyProp.stringValue)) continue;

                    entry = entries.FirstOrDefault(item => item.key == keyProp.stringValue);
                    if (entry.id == SharedTableData.EmptyId) continue;

                    idProp.longValue = entry.id;
                    changed = true;
                }

                if (changed) Apply();
            }

            private void RefreshTableState()
            {
                bool hasTable = collectionIndex >= 0 && collectionIndex < collections.Count;
                bool savedTableMissing = !hasTable && (!string.IsNullOrEmpty(tableKeyProp.stringValue) || !string.IsNullOrEmpty(tableGuidProp.stringValue));
                openButton.SetEnabled(hasTable);
                invalidTableHelp.style.display = savedTableMissing ? DisplayStyle.Flex : DisplayStyle.None;
            }

            private void RefreshEntryUI()
            {
                property.serializedObject.Update();
                rowIndices.Clear();

                for (int i = 0; i < entryKeysProp.arraySize; i++)
                {
                    rowIndices.Add(i);
                }

                listView.itemsSource = rowIndices;
                listView.Rebuild();
                countLabel.text = entryKeysProp.arraySize.ToString();

                bool hasTable = collectionIndex >= 0 && collectionIndex < collections.Count;
                HashSet<long> used = GetUsedEntryIds();
                addButton.SetEnabled(hasTable && entries.Any(entry => !used.Contains(entry.id)));
                emptyLabel.text = hasTable ? "No entries selected." : "Select a localization table first.";
                emptyLabel.style.display = entryKeysProp.arraySize == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                listView.style.display = entryKeysProp.arraySize > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                listView.style.height = Mathf.Min(250f, Mathf.Max(RowHeight, entryKeysProp.arraySize * RowHeight + 2f));
                duplicateHelp.style.display = HasDuplicateEntries() ? DisplayStyle.Flex : DisplayStyle.None;
            }

            private void OpenAddPicker()
            {
                HashSet<long> used = GetUsedEntryIds();
                List<EntryData> available = entries.Where(entry => !used.Contains(entry.id)).ToList();
                ShowEntryPicker(addButton, available, AddEntry);
            }

            private void OpenReplacePicker(int index, VisualElement anchor)
            {
                if (index < 0 || index >= entryIDsProp.arraySize) return;

                long currentId = entryIDsProp.GetArrayElementAtIndex(index).longValue;
                HashSet<long> used = GetUsedEntryIds();
                List<EntryData> available = entries.Where(entry => entry.id == currentId || !used.Contains(entry.id)).ToList();
                ShowEntryPicker(anchor, available, entry => SetEntry(index, entry));
            }

            private static void ShowEntryPicker(VisualElement anchor, List<EntryData> available, Action<EntryData> onSelected)
            {
                if (available.Count == 0) return;

                EntryDropdown dropdown = new(available, onSelected);
                dropdown.Show(anchor.worldBound);
            }

            private void AddEntry(EntryData entry)
            {
                property.serializedObject.Update();

                if (ContainsEntry(entry.id, -1)) return;

                int index = entryKeysProp.arraySize;
                entryKeysProp.arraySize++;
                entryIDsProp.arraySize++;
                entryKeysProp.GetArrayElementAtIndex(index).stringValue = entry.key;
                entryIDsProp.GetArrayElementAtIndex(index).longValue = entry.id;
                Apply();
                RefreshEntryUI();
            }

            private void SetEntry(int index, EntryData entry)
            {
                property.serializedObject.Update();

                if (index < 0 || index >= entryKeysProp.arraySize || ContainsEntry(entry.id, index)) return;

                entryKeysProp.GetArrayElementAtIndex(index).stringValue = entry.key;
                entryIDsProp.GetArrayElementAtIndex(index).longValue = entry.id;
                Apply();
                RefreshEntryUI();
            }

            private void RemoveEntry(int index)
            {
                property.serializedObject.Update();

                if (index < 0 || index >= entryKeysProp.arraySize) return;

                entryKeysProp.DeleteArrayElementAtIndex(index);
                entryIDsProp.DeleteArrayElementAtIndex(index);
                Apply();
                RefreshEntryUI();
            }

            private void MoveEntry(int from, int to)
            {
                property.serializedObject.Update();

                if (from < 0 || to < 0 || from >= entryKeysProp.arraySize || to >= entryKeysProp.arraySize) return;

                entryKeysProp.MoveArrayElement(from, to);
                entryIDsProp.MoveArrayElement(from, to);
                Apply();
                RefreshEntryUI();
            }

            private bool ContainsEntry(long id, int ignoreIndex)
            {
                for (int i = 0; i < entryIDsProp.arraySize; i++)
                {
                    if (i != ignoreIndex && entryIDsProp.GetArrayElementAtIndex(i).longValue == id) return true;
                }

                return false;
            }

            private HashSet<long> GetUsedEntryIds()
            {
                HashSet<long> ids = new();

                for (int i = 0; i < entryIDsProp.arraySize; i++)
                {
                    ids.Add(entryIDsProp.GetArrayElementAtIndex(i).longValue);
                }

                return ids;
            }

            private bool HasDuplicateEntries()
            {
                HashSet<long> ids = new();

                for (int i = 0; i < entryIDsProp.arraySize; i++)
                {
                    long id = entryIDsProp.GetArrayElementAtIndex(i).longValue;
                    if (id != SharedTableData.EmptyId && !ids.Add(id)) return true;
                }

                return false;
            }

            private void ClearSelection()
            {
                tableKeyProp.stringValue = "";
                tableGuidProp.stringValue = "";
                entryKeysProp.ClearArray();
                entryIDsProp.ClearArray();
            }

            private void NormalizeEntryArrays()
            {
                property.serializedObject.Update();

                int previousSize = entryIDsProp.arraySize;
                int targetSize = entryKeysProp.arraySize;
                if (previousSize == targetSize) return;

                entryIDsProp.arraySize = targetSize;
                for (int i = previousSize; i < targetSize; i++) entryIDsProp.GetArrayElementAtIndex(i).longValue = SharedTableData.EmptyId;
                Apply();
            }

            private void OpenSelectedTable()
            {
                if (collectionIndex >= 0 && collectionIndex < collections.Count)
                {
                    LocalizationTablesWindow.ShowWindow(collections[collectionIndex]);
                }
            }

            private void Apply() => property.serializedObject.ApplyModifiedProperties();

            private static void SetFieldLabelWidth<T>(BaseField<T> field, float width)
            {
                field.labelElement.style.minWidth = width;
                field.labelElement.style.width = width;
            }
        }

        private sealed class EntryRow : VisualElement
        {
            public int index = -1;
            public readonly Label indexLabel;
            public readonly Button pickButton;
            public readonly Label previewLabel;
            public readonly Button removeButton;

            public EntryRow()
            {
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;
                style.paddingRight = 2;
                indexLabel = new() { style = { width = 26, unityTextAlign = TextAnchor.MiddleCenter, opacity = 0.55f } };
                VisualElement content = new() { style = { flexGrow = 1, flexShrink = 1, justifyContent = Justify.Center } };
                pickButton = new() { text = "Select entry...", style = { height = 20, unityTextAlign = TextAnchor.MiddleLeft } };
                previewLabel = new() { style = { fontSize = 10, opacity = 0.62f, marginLeft = 4, marginTop = 1, whiteSpace = WhiteSpace.NoWrap } };
                removeButton = new() { text = "×", tooltip = "Remove this entry.", style = { width = 22, height = 20, marginLeft = 4 } };
                content.Add(pickButton);
                content.Add(previewLabel);
                Add(indexLabel);
                Add(content);
                Add(removeButton);
            }
        }

        private sealed class EntryDropdown : AdvancedDropdown
        {
            private readonly IReadOnlyList<EntryData> entries;
            private readonly Action<EntryData> onSelected;

            public EntryDropdown(IReadOnlyList<EntryData> entries, Action<EntryData> onSelected) : base(new AdvancedDropdownState())
            {
                this.entries = entries;
                this.onSelected = onSelected;
                minimumSize = new Vector2(360, 280);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                AdvancedDropdownItem root = new("Entries");

                foreach (EntryData entry in entries)
                {
                    root.AddChild(new EntryDropdownItem(entry));
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is EntryDropdownItem entryItem) onSelected?.Invoke(entryItem.entry);
            }
        }

        private sealed class EntryDropdownItem : AdvancedDropdownItem
        {
            public readonly EntryData entry;
            public EntryDropdownItem(EntryData entry) : base(entry.GetDisplayName()) => this.entry = entry;
        }
    }
}
