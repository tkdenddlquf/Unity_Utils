using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
    /// <summary>Editor window hosting the dialogue graph, localization controls, and node search.</summary>
    public class DialogueEditorWindow : EditorWindow
    {
        private readonly struct NodeSearchResult
        {
            public readonly NodeData Node;
            public readonly string Match;

            public NodeSearchResult(NodeData node, string match)
            {
                Node = node;
                Match = match;
            }
        }

        private sealed class NodeSearchDocument
        {
            public NodeData Node;
            public readonly List<string> Values = new();
            public readonly List<string> LocalizedValues = new();
        }

        private DialogueGraph graph;

        private const long LOCALIZATION_REFRESH_DEBOUNCE_MS = 200;
        private const long SEARCH_REFRESH_DEBOUNCE_MS = 120;

        private IVisualElementScheduledItem localizationRefresh;
        private IVisualElementScheduledItem searchRefresh;

        private VisualElement topRightBar;
        private VisualElement languageBar;
        private VisualElement searchBar;
        private PopupField<Locale> languageDropdown;
        private readonly List<Locale> locales = new();

        private readonly Dictionary<string, List<EntryData>> entryCache = new();
        private readonly List<NodeSearchDocument> searchIndex = new();

        private bool searchIndexDirty = true;

        private string saveData;

        private bool pendingRebuild;

        public IReadOnlyList<LocalizationTableCollection> collections;

        public LocaleIdentifier Language { get; private set; }

        public List<string> Tables { get; } = new();

        private DialogueSO so;
        public DialogueSO SO
        {
            get => so;
            set
            {
                collections = LocalizationEditorSettings.GetStringTableCollections();
                collections.SetTables(Tables);

                CheckSave();

                so = value;

                saveData = JsonUtility.ToJson(value);

                if (value != null)
                {
                    Nodes = value.EditorNodes;
                    Links = value.EditorLinks;

                    graph.UpdateViewTransform(value.position, value.scale);
                }
                else
                {
                    Nodes = null;
                    Links = null;
                }

                ResetView();
            }
        }

        public List<NodeData> Nodes { get; private set; }
        public List<LinkData> Links { get; private set; }

        /// <summary>Initializes the graph, registers undo/localization/event hooks, and builds the UI.</summary>
        private void OnEnable()
        {
            graph = new DialogueGraph(this);
            graph.StretchToParentSize();

            rootVisualElement.Add(graph);

            Undo.undoRedoPerformed -= RequestRebuild;
            Undo.undoRedoPerformed += RequestRebuild;

            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;

            EditorApplication.quitting -= CheckSave;
            EditorApplication.quitting += CheckSave;

            graph.graphViewChanged -= OnGraphViewChanged;
            graph.graphViewChanged += OnGraphViewChanged;

            graph.viewTransformChanged -= OnViewTransformChanged;
            graph.viewTransformChanged += OnViewTransformChanged;

            LocalizationEditorSettings.EditorEvents.CollectionAdded -= OnLocalizationCollectionChanged;
            LocalizationEditorSettings.EditorEvents.CollectionAdded += OnLocalizationCollectionChanged;

            LocalizationEditorSettings.EditorEvents.CollectionRemoved -= OnLocalizationCollectionChanged;
            LocalizationEditorSettings.EditorEvents.CollectionRemoved += OnLocalizationCollectionChanged;

            LocalizationEditorSettings.EditorEvents.TableEntryAdded -= OnLocalizationEntryChanged;
            LocalizationEditorSettings.EditorEvents.TableEntryAdded += OnLocalizationEntryChanged;

            LocalizationEditorSettings.EditorEvents.TableEntryRemoved -= OnLocalizationEntryChanged;
            LocalizationEditorSettings.EditorEvents.TableEntryRemoved += OnLocalizationEntryChanged;

            LocalizationEditorSettings.EditorEvents.LocaleAdded -= OnLocaleChanged;
            LocalizationEditorSettings.EditorEvents.LocaleAdded += OnLocaleChanged;

            LocalizationEditorSettings.EditorEvents.LocaleRemoved -= OnLocaleChanged;
            LocalizationEditorSettings.EditorEvents.LocaleRemoved += OnLocaleChanged;

            graph.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);

            BuildTopRightBar();

            BuildLanguageDropdown();

            SO = SO;
        }

        /// <summary>Detaches the graph and unsubscribes all registered hooks and callbacks.</summary>
        private void OnDisable()
        {
            rootVisualElement.Remove(graph);

            Undo.undoRedoPerformed -= RequestRebuild;

            Undo.postprocessModifications -= OnPostprocessModifications;

            EditorApplication.quitting -= CheckSave;

            graph.graphViewChanged -= OnGraphViewChanged;

            graph.viewTransformChanged -= OnViewTransformChanged;

            LocalizationEditorSettings.EditorEvents.CollectionAdded -= OnLocalizationCollectionChanged;
            LocalizationEditorSettings.EditorEvents.CollectionRemoved -= OnLocalizationCollectionChanged;
            LocalizationEditorSettings.EditorEvents.TableEntryAdded -= OnLocalizationEntryChanged;
            LocalizationEditorSettings.EditorEvents.TableEntryRemoved -= OnLocalizationEntryChanged;

            LocalizationEditorSettings.EditorEvents.LocaleAdded -= OnLocaleChanged;
            LocalizationEditorSettings.EditorEvents.LocaleRemoved -= OnLocaleChanged;

            localizationRefresh?.Pause();
            searchRefresh?.Pause();
            searchRefresh = null;

            graph.UnregisterCallback<KeyDownEvent>(OnKeyDownEvent);

            if (topRightBar != null)
            {
                rootVisualElement.Remove(topRightBar);

                topRightBar = null;
                searchBar = null;
                languageBar = null;
                languageDropdown = null;
            }
        }

        /// <summary>Opens or focuses the dialogue editor window.</summary>
        [MenuItem("Tools/Dialogue")]
        public static DialogueEditorWindow Open() => GetWindow<DialogueEditorWindow>("Dialogue");

        /// <summary>Schedules a localization refresh when a table collection changes.</summary>
        private void OnLocalizationCollectionChanged(LocalizationTableCollection collection) => ScheduleLocalizationRefresh();

        /// <summary>Schedules a localization refresh when a table entry changes.</summary>
        private void OnLocalizationEntryChanged(LocalizationTableCollection collection, SharedTableData.SharedTableEntry entry) => ScheduleLocalizationRefresh();

        /// <summary>Schedules a localization refresh when a locale is added or removed.</summary>
        private void OnLocaleChanged(Locale locale) => ScheduleLocalizationRefresh();

        /// <summary>Debounces and queues a localization refresh.</summary>
        private void ScheduleLocalizationRefresh()
        {
            localizationRefresh ??= rootVisualElement.schedule.Execute(RefreshLocalization);

            localizationRefresh.ExecuteLater(LOCALIZATION_REFRESH_DEBOUNCE_MS);
        }

        /// <summary>Rebuilds the language dropdown and refreshes tables and the view.</summary>
        private void RefreshLocalization()
        {
            searchIndexDirty = true;
            BuildLanguageDropdown();

            if (SO == null) return;

            collections = LocalizationEditorSettings.GetStringTableCollections();
            collections.SetTables(Tables);

            RequestRebuild();
        }

        /// <summary>Rebuilds the language selector bar from the available locales.</summary>
        private void BuildLanguageDropdown()
        {
            if (languageBar != null)
            {
                topRightBar?.Remove(languageBar);

                languageBar = null;
                languageDropdown = null;
            }

            locales.Clear();
            locales.AddRange(LocalizationEditorSettings.GetLocales());

            if (locales.Count == 0)
            {
                Language = default;

                return;
            }

            int index = locales.FindIndex(locale => locale.Identifier == Language);

            if (index < 0)
            {
                LocaleIdentifier system = Application.systemLanguage;

                index = locales.FindIndex(locale => locale.Identifier == system);

                if (index < 0) index = 0;
            }

            Language = locales[index].Identifier;

            languageBar = new VisualElement();

            StyleBar(languageBar);

            Label label = new("Language")
            {
                style =
                {
                    marginRight = 6,
                    color = new Color(0.78f, 0.78f, 0.78f),
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };

            languageDropdown = new PopupField<Locale>(locales, index, FormatLocale, FormatLocale);

            languageDropdown.style.minWidth = 110;

            languageDropdown.style.marginLeft = 0;
            languageDropdown.style.marginRight = 0;
            languageDropdown.style.marginTop = 0;
            languageDropdown.style.marginBottom = 0;

            languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);

            languageBar.Add(label);
            languageBar.Add(languageDropdown);

            topRightBar.Insert(System.Math.Min(1, topRightBar.childCount), languageBar);
        }

        /// <summary>Creates the top-right toolbar stack and the node-search bar.</summary>
        private void BuildTopRightBar()
        {
            topRightBar = new VisualElement();

            topRightBar.style.position = Position.Absolute;
            topRightBar.style.top = 12;
            topRightBar.style.right = 12;
            topRightBar.style.width = 420;

            topRightBar.style.flexDirection = FlexDirection.Column;
            topRightBar.style.alignItems = Align.Stretch;
            topRightBar.style.overflow = Overflow.Visible;

            topRightBar.style.paddingLeft = 10;
            topRightBar.style.paddingRight = 10;
            topRightBar.style.paddingTop = 8;
            topRightBar.style.paddingBottom = 10;

            topRightBar.style.backgroundColor = new Color(0.075f, 0.085f, 0.105f, 0.97f);

            topRightBar.style.borderTopLeftRadius = 8;
            topRightBar.style.borderTopRightRadius = 8;
            topRightBar.style.borderBottomLeftRadius = 8;
            topRightBar.style.borderBottomRightRadius = 8;
            topRightBar.style.borderTopWidth = 1;
            topRightBar.style.borderRightWidth = 1;
            topRightBar.style.borderBottomWidth = 1;
            topRightBar.style.borderLeftWidth = 1;
            topRightBar.style.borderTopColor = new Color(0.22f, 0.26f, 0.34f);
            topRightBar.style.borderRightColor = new Color(0.22f, 0.26f, 0.34f);
            topRightBar.style.borderBottomColor = new Color(0.22f, 0.26f, 0.34f);
            topRightBar.style.borderLeftColor = new Color(0.22f, 0.26f, 0.34f);

            rootVisualElement.Add(topRightBar);

            Label toolbarTitle = new("Dialogue Tools");
            toolbarTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbarTitle.style.fontSize = 13;
            toolbarTitle.style.color = new Color(0.88f, 0.92f, 1f);
            toolbarTitle.style.marginBottom = 4;
            topRightBar.Add(toolbarTitle);

            searchBar = new VisualElement();

            StyleBar(searchBar);

            searchBar.style.flexDirection = FlexDirection.Column;
            searchBar.style.alignItems = Align.Stretch;
            searchBar.style.marginTop = 6;
            searchBar.style.overflow = Overflow.Visible;

            VisualElement inputRow = new();

            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems = Align.Center;

            Label searchLabel = new("Search Nodes")
            {
                style =
                {
                    marginRight = 6,
                    color = new Color(0.78f, 0.78f, 0.78f),
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };

            TextField searchField = new()
            {
                name = "node-search",
                tooltip = "Search by node ID, node type, or a value stored inside a node.",
            };
            searchField.style.flexGrow = 1;
            searchField.style.minWidth = 220;
            searchField.style.marginLeft = 0;
            searchField.style.marginRight = 4;
            searchField.style.marginTop = 0;
            searchField.style.marginBottom = 0;

            ScrollView suggestions = new(ScrollViewMode.Vertical) { name = "node-suggestions" };

            suggestions.style.display = DisplayStyle.None;
            suggestions.style.marginTop = 4;
            suggestions.style.maxHeight = 320;
            suggestions.style.minWidth = 400;
            suggestions.style.maxWidth = 400;
            suggestions.style.backgroundColor = new Color(0.045f, 0.05f, 0.065f, 1f);
            suggestions.style.borderTopWidth = 1;
            suggestions.style.borderRightWidth = 1;
            suggestions.style.borderBottomWidth = 1;
            suggestions.style.borderLeftWidth = 1;
            suggestions.style.borderTopColor = new Color(0.35f, 0.35f, 0.35f);
            suggestions.style.borderRightColor = new Color(0.35f, 0.35f, 0.35f);
            suggestions.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f);
            suggestions.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f);

            searchField.RegisterValueChangedCallback(_ =>
            {
                searchRefresh ??= searchField.schedule.Execute(() => RefreshSuggestions(searchField, suggestions, searchField.value));
                searchRefresh.ExecuteLater(SEARCH_REFRESH_DEBOUNCE_MS);
            });

            searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;

                SearchNode(searchField, suggestions);

                evt.StopPropagation();
            });

            searchField.RegisterCallback<FocusOutEvent>(_ => suggestions.schedule.Execute(() => HideSuggestions(suggestions)).StartingIn(150));

            Button searchButton = new(() => SearchNode(searchField, suggestions)) { text = "Go" };

            searchButton.style.marginLeft = 0;
            searchButton.style.marginRight = 0;
            searchButton.style.marginTop = 0;
            searchButton.style.marginBottom = 0;

            inputRow.Add(searchLabel);
            inputRow.Add(searchField);
            inputRow.Add(searchButton);

            searchBar.Add(inputRow);
            searchBar.Add(suggestions);

            topRightBar.Add(searchBar);

            VisualElement repairBar = new();

            StyleBar(repairBar);

            repairBar.style.justifyContent = Justify.FlexEnd;
            repairBar.style.marginTop = 6;

            Button refreshButton = new(RefreshEditor)
            {
                text = "↻  Refresh",
                tooltip = "Refresh added or changed assets, localization data, command schemas, and the graph view.",
            };

            refreshButton.style.marginLeft = 0;
            refreshButton.style.marginRight = 4;
            refreshButton.style.marginTop = 0;
            refreshButton.style.marginBottom = 0;

            Button repairButton = new(RepairData)
            {
                text = "✓  Validate / Repair",
                tooltip = "Finds and repairs broken links, duplicates, orphaned nodes, and other graph data issues.",
            };

            repairButton.style.marginLeft = 0;
            repairButton.style.marginRight = 0;
            repairButton.style.marginTop = 0;
            repairButton.style.marginBottom = 0;

            repairBar.Add(refreshButton);
            repairBar.Add(repairButton);

            topRightBar.Add(repairBar);

            // Search results can overlap the rows below; draw this bar last so the list stays visible.
            searchBar.BringToFront();
        }

        /// <summary>Lays a toolbar row out horizontally; the shared dark background is applied once on the container.</summary>
        private static void StyleBar(VisualElement bar)
        {
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
        }

        /// <summary>Imports changed assets and rebuilds all editor-side dialogue data and views.</summary>
        private void RefreshEditor()
        {
            AssetDatabase.Refresh();
            DialogueVariableSchemaUtility.Invalidate();
            RefreshLocalization();
        }

        private const int MAX_SUGGESTIONS = 10;

        /// <summary>Rebuilds the autocomplete list from node ids, types, and stored values matching the typed text.</summary>
        private void RefreshSuggestions(TextField field, VisualElement container, string rawQuery)
        {
            container.Clear();

            string query = rawQuery?.Trim();

            if (SO == null || string.IsNullOrEmpty(query))
            {
                HideSuggestions(container);

                return;
            }

            int shown = 0;

            EnsureSearchIndex();

            foreach (NodeSearchDocument document in searchIndex)
            {
                if (!TryMatchNode(document, query, out NodeSearchResult result)) continue;

                container.Add(MakeSuggestion(field, container, result));

                if (++shown >= MAX_SUGGESTIONS) break;
            }

            container.style.display = shown > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>One clickable row in the autocomplete list.</summary>
        private VisualElement MakeSuggestion(TextField field, VisualElement container, NodeSearchResult result)
        {
            VisualElement item = new();
            VisualElement header = new();
            Label type = new(result.Node.type.ToString());
            Label id = new(result.Node.guid);
            Label match = new(result.Match);

            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            type.style.unityFontStyleAndWeight = FontStyle.Bold;
            type.style.color = new Color(0.62f, 0.78f, 1f);
            type.style.marginRight = 8;

            id.style.flexGrow = 1;
            id.style.color = new Color(0.72f, 0.75f, 0.8f);
            id.style.whiteSpace = WhiteSpace.NoWrap;

            match.style.marginTop = 3;
            match.style.color = new Color(0.9f, 0.9f, 0.92f);
            match.style.whiteSpace = WhiteSpace.Normal;

            header.Add(type);
            header.Add(id);
            item.Add(header);
            item.Add(match);

            item.style.paddingLeft = 9;
            item.style.paddingRight = 9;
            item.style.paddingTop = 7;
            item.style.paddingBottom = 7;
            item.style.marginLeft = 3;
            item.style.marginRight = 3;
            item.style.marginTop = 2;
            item.style.marginBottom = 2;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new Color(0.18f, 0.2f, 0.25f);

            Color hover = new(0.18f, 0.28f, 0.44f, 0.75f);

            item.RegisterCallback<MouseEnterEvent>(_ => item.style.backgroundColor = hover);
            item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = StyleKeyword.Null);

            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                field.SetValueWithoutNotify(result.Node.guid);

                HideSuggestions(container);

                FocusNode(result.Node.guid, true);

                evt.StopPropagation();
            });

            return item;
        }

        /// <summary>Clears and hides the suggestion list.</summary>
        private static void HideSuggestions(VisualElement container)
        {
            container.Clear();

            container.style.display = DisplayStyle.None;
        }

        /// <summary>Jumps to the node matching the query, preferring an exact id over the first suggestion.</summary>
        private void SearchNode(TextField field, VisualElement suggestions)
        {
            if (SO == null) return;

            string query = field.value?.Trim();

            if (string.IsNullOrEmpty(query)) return;

            NodeData exact = GetNode(query);

            string guid = !string.IsNullOrEmpty(exact.guid) ? exact.guid : FirstSuggestion(query);

            if (guid == null)
            {
                FlashInvalid(field);

                return;
            }

            field.SetValueWithoutNotify(guid);

            HideSuggestions(suggestions);

            FocusNode(guid, true);
        }

        /// <summary>Returns the first node matching by id, type, or stored value.</summary>
        private string FirstSuggestion(string query)
        {
            EnsureSearchIndex();

            foreach (NodeSearchDocument document in searchIndex)
            {
                if (TryMatchNode(document, query, out _)) return document.Node.guid;
            }

            return null;
        }

        /// <summary>Returns all nodes including the start node, for search and autocomplete.</summary>
        private List<NodeData> AllNodes()
        {
            List<NodeData> nodes = new();

            if (SO == null) return nodes;

            if (!string.IsNullOrEmpty(SO.StartGuid)) nodes.Add(SO.EditorStartNode);

            for (int i = 0; i < Nodes.Count; i++)
            {
                if (!string.IsNullOrEmpty(Nodes[i].guid)) nodes.Add(Nodes[i]);
            }

            return nodes;
        }

        /// <summary>Builds the expensive node/localization search data once after relevant editor changes.</summary>
        private void EnsureSearchIndex()
        {
            if (!searchIndexDirty) return;

            searchIndex.Clear();

            foreach (NodeData node in AllNodes())
            {
                NodeSearchDocument document = new() { Node = node };

                CollectDataValues(node.PortDatas, document.Values);
                CollectDataValues(node.OptionDatas, document.Values);
                CollectLocalizedValues(node, document.LocalizedValues);

                searchIndex.Add(document);
            }

            searchIndexDirty = false;
        }

        private static void CollectDataValues(IReadOnlyList<DataWrapper> wrappers, List<string> target)
        {
            if (wrappers == null) return;

            for (int i = 0; i < wrappers.Count; i++)
            {
                List<GenericData> values = wrappers[i].data;

                if (values == null) continue;

                for (int j = 0; j < values.Count; j++)
                {
                    string value = values[j].ToString();

                    if (!string.IsNullOrEmpty(value)) target.Add(value);
                }
            }
        }

        /// <summary>Finds the highest-priority matching part of an indexed node.</summary>
        private static bool TryMatchNode(NodeSearchDocument document, string query, out NodeSearchResult result)
        {
            NodeData node = document.Node;

            if (Contains(node.guid, query))
            {
                result = new NodeSearchResult(node, $"ID: {node.guid}");
                return true;
            }

            string type = node.type.ToString();

            if (Contains(type, query))
            {
                result = new NodeSearchResult(node, $"Type: {type}");
                return true;
            }

            if (TryMatchValues(document.LocalizedValues, query, out string localizedText))
            {
                result = new NodeSearchResult(node, $"Localized: {Shorten(localizedText)}");
                return true;
            }

            if (TryMatchValues(document.Values, query, out string value))
            {
                result = new NodeSearchResult(node, $"Value: {Shorten(value)}");
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>Indexes localized speaker, dialogue, and choice text in the currently selected language.</summary>
        private void CollectLocalizedValues(NodeData node, List<string> target)
        {
            IReadOnlyList<DataWrapper> options = node.OptionDatas;

            if (node.type == NodeType.Dialogue && options != null && options.Count >= 4)
            {
                AddLocalizedEntry(options[0], options[1], target);
                AddLocalizedEntry(options[2], options[3], target);
            }
            else if (node.type == NodeType.Choice && options != null && options.Count >= 3)
            {
                AddLocalizedEntry(options[0], options[1], target);

                IReadOnlyList<DataWrapper> ports = node.PortDatas;

                if (ports != null)
                {
                    for (int i = 0; i < ports.Count; i++) AddLocalizedEntry(options[2], ports[i], target);
                }
            }
        }

        private void AddLocalizedEntry(DataWrapper tableWrapper, DataWrapper entryWrapper, List<string> target)
        {
            List<GenericData> tableData = tableWrapper.data;
            List<GenericData> entryData = entryWrapper.data;

            if (tableData == null || tableData.Count == 0 || entryData == null || entryData.Count < 2)
                return;

            string tableName = tableData[0].ToString();
            string key = entryData[0].ToString();

            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(key))
                return;

            for (int i = 0; collections != null && i < collections.Count; i++)
            {
                LocalizationTableCollection collection = collections[i];

                if (collection.TableCollectionName != tableName) continue;

                string text = new EntryData(entryData[1].GetLong(), key, collection.Tables).GetText(Language);

                if (!string.IsNullOrEmpty(text)) target.Add(text);
                return;
            }
        }

        private static bool TryMatchValues(IReadOnlyList<string> values, string query, out string match)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (!Contains(values[i], query)) continue;

                match = values[i];
                return true;
            }

            match = null;
            return false;
        }

        private static bool Contains(string value, string query) =>
            value?.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Shorten(string value)
        {
            const int maxLength = 60;

            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? "";

            return value.Substring(0, maxLength - 1) + "…";
        }

        /// <summary>Briefly tints the search field red to signal no matching node.</summary>
        private static void FlashInvalid(TextField field)
        {
            VisualElement input = field.Q("unity-text-input") ?? field;

            input.style.backgroundColor = new Color(0.55f, 0.20f, 0.20f);

            input.schedule.Execute(() => input.style.backgroundColor = StyleKeyword.Null).StartingIn(700);
        }

        /// <summary>Returns the display name for a locale, or empty if null.</summary>
        private static string FormatLocale(Locale locale) => locale == null ? "" : locale.LocaleName;

        /// <summary>Updates the active language and refreshes the view when the dropdown changes.</summary>
        private void OnLanguageChanged(ChangeEvent<Locale> evt)
        {
            if (evt.newValue == null) return;

            Language = evt.newValue.Identifier;

            ResetView();
        }

        /// <summary>Fills the target list with the collection's entries, caching per collection.</summary>
        public void GetEntriesInto(LocalizationTableCollection collection, List<EntryData> target)
        {
            target.Clear();

            if (collection == null) return;

            if (!entryCache.TryGetValue(collection.TableCollectionName, out List<EntryData> cached))
            {
                cached = new List<EntryData>();

                collection.SetEntries(cached);

                entryCache[collection.TableCollectionName] = cached;
            }

            target.AddRange(cached);
        }

        /// <summary>Saves changes on Ctrl+S.</summary>
        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.ctrlKey && evt.keyCode == KeyCode.S)
            {
                SaveChanges();

                evt.StopPropagation();
            }
        }

        /// <summary>Prompts to save or discard when there are unsaved changes.</summary>
        private void CheckSave()
        {
            if (!hasUnsavedChanges) return;

            bool save = EditorUtility.DisplayDialog(
                "Unsaved Changes",
                "There are unsaved changes. Do you want to save them before quitting?",
                "Save",
                "Don't Save"
            );

            if (save) SaveChanges();
            else DiscardChanges();
        }

        /// <summary>Persists the dialogue asset and captures its serialized state.</summary>
        public override void SaveChanges()
        {
            if (SO == null) return;

            AssetDatabase.SaveAssetIfDirty(SO);

            saveData = JsonUtility.ToJson(SO);

            base.SaveChanges();
        }

        /// <summary>Reverts the dialogue asset to the last saved serialized state.</summary>
        public override void DiscardChanges()
        {
            if (SO == null) return;

            JsonUtility.FromJsonOverwrite(saveData, SO);

            ResetView();

            base.DiscardChanges();
        }

        /// <summary>Marks the window as having unsaved changes.</summary>
        public void SetUnsaved()
        {
            hasUnsavedChanges = true;
            searchIndexDirty = true;
        }

        /// <summary>Refreshes the view when undo-tracked condition or event properties change.</summary>
        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] mods)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                UndoPropertyModification mod = mods[i];
                PropertyModification current = mod.currentValue;

                string path = current.propertyPath;

                if (current.target == SO && (path.StartsWith("conditions") || path.StartsWith("events")))
                {
                    RequestRebuild();

                    break;
                }
            }

            return mods;
        }

        /// <summary>Stores the graph's pan/zoom into the asset and flags unsaved changes.</summary>
        private void OnViewTransformChanged(GraphView graphView)
        {
            if (SO == null) return;

            IResolvedStyle style = graph.contentViewContainer.resolvedStyle;

            Vector3 position = style.translate;
            Vector3 scale = style.scale.value;

            if (SO.position == position && SO.scale == scale) return;

            SO.position = position;
            SO.scale = scale;

            hasUnsavedChanges = true;
        }

        #region View
        /// <summary>Re-tints the graph's port colors to reflect current links.</summary>
        public void RefreshPortColors() => graph?.RefreshPortColors();

        /// <summary>Pans the graph so the given node is centered.</summary>
        public void FocusNode(string guid, bool select = false) => graph?.FocusNode(guid, select);

        /// <summary>Disconnects and removes every edge attached to a port.</summary>
        public void RemoveEdge(Port port)
        {
            IEnumerator<Edge> enumerator = port.connections.GetEnumerator();

            while (enumerator.MoveNext())
            {
                Edge edge = enumerator.Current;

                if (edge != null)
                {
                    edge.input?.Disconnect(edge);
                    edge.output?.Disconnect(edge);

                    graph.RemoveElement(edge);

                    enumerator.Dispose();
                    enumerator = port.connections.GetEnumerator();
                }
            }
        }

        /// <summary>
        /// Rebuilds the graph immediately when the window is focused; otherwise defers the rebuild
        /// until the window regains focus so background editor events don't force off-screen rebuilds.
        /// </summary>
        private void RequestRebuild()
        {
            if (hasFocus) ResetView();
            else pendingRebuild = true;
        }

        /// <summary>Flushes any rebuild that was deferred while the window was unfocused.</summary>
        private void OnFocus()
        {
            if (!pendingRebuild) return;

            pendingRebuild = false;

            ResetView();
        }

        /// <summary>Clears caches, ensures a start node exists, and rebuilds the graph.</summary>
        private void ResetView()
        {
            pendingRebuild = false;
            searchIndexDirty = true;

            entryCache.Clear();

            if (SO != null)
            {
                NodeData startNode = SO.EditorStartNode;

                if (string.IsNullOrEmpty(startNode.guid))
                {
                    SO.EditorStartNode = new NodeData(NodeType.Start);

                    EditorUtility.SetDirty(SO);
                }
            }

            graph.RebuildAll();

            graph.ClearSelection();
            graph.MarkDirtyRepaint();
        }

        /// <summary>Applies graph edits (edge/node create, remove, move) back into the data and marks dirty.</summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (SO == null) return change;

            if (change.edgesToCreate != null) CreateEdge(change.edgesToCreate);

            if (change.elementsToRemove != null)
            {
                RemoveNode(change.elementsToRemove);

                graph.RequestSync();
            }

            if (change.movedElements != null) MoveNode(change.movedElements);

            graph.RefreshPortColors();

            EditorUtility.SetDirty(SO);

            SetUnsaved();

            return change;
        }
        #endregion

        #region Data
        /// <summary>Returns the node data for a guid, or default if not found.</summary>
        public NodeData GetNode(string guid)
        {
            if (guid == SO.StartGuid) return SO.EditorStartNode;

            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].guid == guid) return Nodes[i];
            }

            return default;
        }

        /// <summary>Replaces the stored node data for the given guid.</summary>
        public void SetNode(string guid, NodeData data)
        {
            if (SO.StartGuid == guid) SO.EditorStartNode = data;
            else
            {
                for (int i = 0; i < Nodes.Count; i++)
                {
                    if (Nodes[i].guid == guid) Nodes[i] = data;
                }
            }
        }

        /// <summary>Adds link data for each new edge, enforcing one connection per output port.</summary>
        public void CreateEdge(List<Edge> selectable)
        {
            Undo.RecordObject(SO, "Create Edge");

            foreach (Edge edge in selectable)
            {
                LinkData link = CreateLink(edge);

                if (string.IsNullOrEmpty(link.nodeGuid) || string.IsNullOrEmpty(link.targetGuid)) continue;

                // Output ports are Single-capacity, but virtualization can hide an existing edge so the
                // port looks unconnected and GraphView lets a second edge be dragged. Drop any prior link
                // from the same output port before adding the new one to keep one connection per port.
                for (int i = Links.Count - 1; i >= 0; i--)
                {
                    if (Links[i].nodeGuid == link.nodeGuid && Links[i].outPortIndex == link.outPortIndex) Links.RemoveAt(i);
                }

                Links.Add(link);
            }
        }

        /// <summary>Removes the selected edges' links and nodes (except the start node) from the data.</summary>
        public void RemoveNode<T>(List<T> selectable) where T : ISelectable
        {
            Undo.RecordObject(SO, "Remove Node");

            foreach (T element in selectable)
            {
                if (element is Edge edge)
                {
                    LinkData link = CreateLink(edge);

                    if (Links.Contains(link)) Links.Remove(link);
                }
                else if (element is BaseNode node)
                {
                    if (node.GUID == SO.StartGuid) continue;

                    for (int i = 0; i < Nodes.Count; i++)
                    {
                        if (Nodes[i].guid == node.GUID)
                        {
                            Nodes.RemoveAt(i);

                            break;
                        }
                    }

                    for (int i = Links.Count - 1; i >= 0; i--)
                    {
                        if (Links[i].nodeGuid == node.GUID || Links[i].targetGuid == node.GUID) Links.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Scans the loaded graph for data/view defects and repairs them in place: drops empty/duplicate-guid nodes,
        /// nodes colliding with the start node, and links that are empty, out of range, dangling, or duplicated.
        /// Then rebuilds the view from the cleaned data and reports what changed.
        /// </summary>
        public void RepairData()
        {
            if (SO == null)
            {
                EditorUtility.DisplayDialog("Dialogue Repair", "There is no Dialogue asset to validate.", "OK");

                return;
            }

            Undo.RecordObject(SO, "Repair Dialogue Data");

            int removedNodes = 0;
            int removedLinks = 0;

            string startGuid = SO.StartGuid;

            // Pass 1 — clean nodes and build the set of valid node guids.
            HashSet<string> validGuids = new();

            if (!string.IsNullOrEmpty(startGuid)) validGuids.Add(startGuid);

            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                string guid = Nodes[i].guid;

                if (string.IsNullOrEmpty(guid) || guid == startGuid || !validGuids.Add(guid))
                {
                    Nodes.RemoveAt(i);

                    removedNodes++;
                }
            }

            // Pass 2 — drop links that are empty, negative-port, dangling on either end, duplicates, or a
            // second connection from an output port that is already linked (only one per Single-capacity port).
            HashSet<LinkData> seenLinks = new();
            HashSet<(string, int)> seenPorts = new();

            for (int i = Links.Count - 1; i >= 0; i--)
            {
                LinkData link = Links[i];

                bool invalid = string.IsNullOrEmpty(link.nodeGuid) || string.IsNullOrEmpty(link.targetGuid) ||
                               link.outPortIndex < 0 ||
                               !validGuids.Contains(link.nodeGuid) || !validGuids.Contains(link.targetGuid) ||
                               !seenLinks.Add(link);

                // Among otherwise-valid links, keep only the newest (highest-index) link per output port.
                if (!invalid && !seenPorts.Add((link.nodeGuid, link.outPortIndex))) invalid = true;

                if (invalid)
                {
                    Links.RemoveAt(i);

                    removedLinks++;
                }
            }

            EditorUtility.SetDirty(SO);

            SetUnsaved();

            // Rebuild the view so on-screen nodes/edges match the cleaned data.
            graph.RebuildAll();

            if (removedNodes == 0 && removedLinks == 0)
            {
                EditorUtility.DisplayDialog("Dialogue Repair", "No issues were found.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Dialogue Repair", $"Repair complete.\n\n• Removed nodes: {removedNodes}\n• Removed links: {removedLinks}\n\nSave the changes with Ctrl+S.", "OK");
            }
        }

        /// <summary>Writes moved nodes' new positions back into the stored node data.</summary>
        private void MoveNode<T>(List<T> selectable) where T : ISelectable
        {
            Undo.RecordObject(SO, "Move Node");

            foreach (T element in selectable)
            {
                if (element is BaseNode node)
                {
                    node.GraphPosition = node.GetPosition().position;

                    if (node.GUID == SO.StartGuid)
                    {
                        NodeData data = SO.EditorStartNode;

                        data.position = node.GetPosition().position;

                        SO.EditorStartNode = data;
                    }
                    else
                    {
                        for (int i = 0; i < Nodes.Count; i++)
                        {
                            if (Nodes[i].guid == node.GUID)
                            {
                                NodeData data = Nodes[i];

                                data.position = node.GetPosition().position;

                                Nodes[i] = data;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Builds link data from an edge's endpoint nodes and output port index.</summary>
        private LinkData CreateLink(Edge edge)
        {
            Port outputPort = edge.output;
            Port inputPort = edge.input;

            if (outputPort.node is BaseNode outputNode && inputPort.node is BaseNode inputNode)
            {
                NodeData outputData = GetNode(outputNode.GUID);
                NodeData inputData = GetNode(inputNode.GUID);

                LinkData link = new()
                {
                    nodeGuid = outputData.guid,
                    targetGuid = inputData.guid,

                    outPortIndex = outputPort.parent.IndexOf(outputPort),
                };

                return link;
            }

            return default;
        }

        /// <summary>Finds the on-screen node whose data matches the guid, returning it and its data.</summary>
        private BaseNode GetLinkedNode(string guid, out NodeData data)
        {
            foreach (Node node in graph.nodes)
            {
                if (node is BaseNode baseNode)
                {
                    data = GetNode(baseNode.GUID);

                    if (data.guid == guid) return baseNode;
                }
            }

            data = default;

            return null;
        }
        #endregion
    }
}
