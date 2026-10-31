using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace Yang.Dialogue.Editor
{
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

        [System.Serializable]
        private sealed class ViewState
        {
            public Vector3 position;
            public Vector3 scale;
        }

        private readonly struct NavigationState
        {
            public readonly Vector3 Position;
            public readonly Vector3 Scale;
            public readonly string SelectedGuid;

            public NavigationState(Vector3 position, Vector3 scale, string selectedGuid)
            {
                Position = position;
                Scale = scale;
                SelectedGuid = selectedGuid;
            }
        }

        [System.NonSerialized] public IReadOnlyList<LocalizationTableCollection> collections;

        private DialogueGraph graph;
        private IVisualElementScheduledItem localizationRefresh;
        private IVisualElementScheduledItem searchRefresh;
        private IVisualElementScheduledItem viewSave;
        private string pendingViewKey;
        private ViewState pendingViewState;
        private VisualElement topRightBar;
        private VisualElement languageBar;
        private VisualElement searchBar;
        private VisualElement repairBar;
        private VisualElement debugBar;
        private VisualElement assetBar;
        private ObjectField debugRunnerField;
        private PopupField<string> debugFlowField;
        private TextField debugKeyField;
        private Label debugStatus;
        private Label debugApplyStatus;
        private Button debugApplyButton;
        private Toggle debugFollow;
        private DialogueRunner debugRunner;
        private double nextDebugRefresh;
        private double nextRunnerLookup;
        private double nextGraphDiffCheck;
        private bool pendingGraphChanges;
        private bool debugJumpInProgress;
        private string lastDebugFollowGuid;
        private bool wasPlaying;
        private Label emptyGraphMessage;
        private Label toolbarTitle;
        private Button collapseButton;
        private ObjectField assetField;
        private Button backButton;
        private Button forwardButton;
        private TextField searchField;
        private ScrollView searchSuggestions;
        private bool toolsCollapsed;
        private string matchingQuery;
        private int matchingIndex = -1;
        private Label searchStatus;
        private int selectedSuggestion = -1;
        private PopupField<Locale> languageDropdown;
        private bool searchIndexDirty = true;
        private bool nodeIndicesDirty = true;
        private string saveData;
        private bool pendingRebuild;
        private bool rebuildingGraph;
        private (int nodes, int references, int updated, int unresolved) lastLocalizationSync;
        private Button refreshButton;
        private DialogueSO so;

        private const long LOCALIZATION_REFRESH_DEBOUNCE_MS = 200;
        private const long SEARCH_REFRESH_DEBOUNCE_MS = 120;
        private const long VIEW_SAVE_DEBOUNCE_MS = 250;
        private const int NAVIGATION_LIMIT = 50;

        private readonly List<DialogueFlowState> debugFlows = new();
        private readonly List<NodeSearchResult> searchMatches = new();
        private readonly List<NodeSearchResult> suggestionResults = new();
        private readonly List<VisualElement> suggestionItems = new();
        private readonly List<NavigationState> backHistory = new();
        private readonly List<NavigationState> forwardHistory = new();
        private readonly List<Locale> locales = new();
        private readonly List<NodeSearchDocument> searchIndex = new();
        private readonly Dictionary<string, int> nodeIndices = new();
        private readonly List<string> localizationIssues = new();

        public LocaleIdentifier Language { get; private set; }
        public List<string> Tables { get; } = new();

        public DialogueSO SO
        {
            get => so;
            set
            {
                FlushViewState();

                if (so != value)
                {
                    CheckSave();
                    graph?.ClearSelection();
                    backHistory.Clear();
                    forwardHistory.Clear();
                    RefreshNavigationButtons();
                }

                collections = LocalizationEditorSettings.GetStringTableCollections();
                collections.SetTables(Tables);
                so = value;
                assetField?.SetValueWithoutNotify(value);
                InvalidateNodeIndices();
                searchIndexDirty = true;
                matchingQuery = null;
                saveData = value == null ? null : JsonUtility.ToJson(value);
                graph.SetDebugNode(null);
                lastDebugFollowGuid = null;
                UpdateEmptyGraphMessage();

                if (value != null)
                {
                    SynchronizeLocalizationReferences();
                    ViewState state = ReadViewState(value);
                    graph.UpdateViewTransform(state.position, state.scale);
                }

                ResetView();
            }
        }

        public List<NodeData> Nodes => SO?.EditorNodes;
        public List<LinkData> Links => SO?.EditorLinks;

        private void OnEnable()
        {
            graph = new DialogueGraph(this);
            graph.StretchToParentSize();
            rootVisualElement.Add(graph);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);

            Undo.undoRedoPerformed -= RequestRebuild;
            Undo.undoRedoPerformed += RequestRebuild;
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.postprocessModifications += OnPostprocessModifications;

            EditorApplication.quitting -= CheckSave;
            EditorApplication.quitting += CheckSave;
            EditorApplication.projectChanged -= ScheduleLocalizationRefresh;
            EditorApplication.projectChanged += ScheduleLocalizationRefresh;

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
            LocalizationEditorSettings.EditorEvents.TableEntryModified -= OnLocalizationEntryModified;
            LocalizationEditorSettings.EditorEvents.TableEntryModified += OnLocalizationEntryModified;
            LocalizationEditorSettings.EditorEvents.LocaleAdded -= OnLocaleChanged;
            LocalizationEditorSettings.EditorEvents.LocaleAdded += OnLocaleChanged;
            LocalizationEditorSettings.EditorEvents.LocaleRemoved -= OnLocaleChanged;
            LocalizationEditorSettings.EditorEvents.LocaleRemoved += OnLocaleChanged;

            graph.RegisterCallback<KeyDownEvent>(OnKeyDownEvent);
            BuildEmptyGraphMessage();
            BuildTopRightBar();
            BuildLanguageDropdown();
            SO = SO;
        }

        private void OnDisable()
        {
            FlushViewState();
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            rootVisualElement.Remove(graph);

            Undo.undoRedoPerformed -= RequestRebuild;
            Undo.postprocessModifications -= OnPostprocessModifications;

            EditorApplication.quitting -= CheckSave;
            EditorApplication.projectChanged -= ScheduleLocalizationRefresh;

            graph.graphViewChanged -= OnGraphViewChanged;
            graph.viewTransformChanged -= OnViewTransformChanged;

            LocalizationEditorSettings.EditorEvents.CollectionAdded -= OnLocalizationCollectionChanged;
            LocalizationEditorSettings.EditorEvents.CollectionRemoved -= OnLocalizationCollectionChanged;
            LocalizationEditorSettings.EditorEvents.TableEntryAdded -= OnLocalizationEntryChanged;
            LocalizationEditorSettings.EditorEvents.TableEntryRemoved -= OnLocalizationEntryChanged;
            LocalizationEditorSettings.EditorEvents.TableEntryModified -= OnLocalizationEntryModified;
            LocalizationEditorSettings.EditorEvents.LocaleAdded -= OnLocaleChanged;
            LocalizationEditorSettings.EditorEvents.LocaleRemoved -= OnLocaleChanged;

            localizationRefresh?.Pause();
            searchRefresh?.Pause();
            viewSave?.Pause();
            searchRefresh = null;
            debugRunner = null;
            debugFlows.Clear();

            if (emptyGraphMessage != null) rootVisualElement.Remove(emptyGraphMessage);

            emptyGraphMessage = null;
            viewSave = null;
            graph.UnregisterCallback<KeyDownEvent>(OnKeyDownEvent);

            if (topRightBar != null)
            {
                rootVisualElement.Remove(topRightBar);
                topRightBar = null;
                searchBar = null;
                languageBar = null;
                languageDropdown = null;
                assetBar = null;
                assetField = null;
                collapseButton = null;
                repairBar = null;
                debugBar = null;
                debugRunnerField = null;
                debugFlowField = null;
                debugKeyField = null;
                debugStatus = null;
                debugApplyStatus = null;
                debugApplyButton = null;
                debugFollow = null;
                searchField = null;
                searchSuggestions = null;
                searchStatus = null;
                backButton = null;
                forwardButton = null;
            }
        }

        [MenuItem("Tools/Dialogue")]
        public static DialogueEditorWindow Open() => GetWindow<DialogueEditorWindow>("Dialogue");

        private void OnLocalizationCollectionChanged(LocalizationTableCollection collection) => ScheduleLocalizationRefresh();

        private void OnLocalizationEntryChanged(LocalizationTableCollection collection, SharedTableData.SharedTableEntry entry) => ScheduleLocalizationRefresh();

        private void OnLocalizationEntryModified(SharedTableData.SharedTableEntry entry) => ScheduleLocalizationRefresh();

        private void OnLocaleChanged(Locale locale) => ScheduleLocalizationRefresh();

        private void ScheduleLocalizationRefresh()
        {
            localizationRefresh ??= rootVisualElement.schedule.Execute(RefreshLocalization);
            localizationRefresh.ExecuteLater(LOCALIZATION_REFRESH_DEBOUNCE_MS);
        }

        private void RefreshLocalization()
        {
            searchIndexDirty = true;
            BuildLanguageDropdown();

            if (SO == null) return;

            collections = LocalizationEditorSettings.GetStringTableCollections();
            collections.SetTables(Tables);
            lastLocalizationSync = SynchronizeLocalizationReferences();
            RequestRebuild();
        }

        private (int nodes, int references, int updated, int unresolved) SynchronizeLocalizationReferences()
        {
            if (SO == null) return default;

            localizationIssues.Clear();
            int scanned = 0, references = 0, updated = 0, unresolved = 0;
            bool recorded = false;

            for (int i = 0; i < Nodes.Count; i++)
            {
                NodeData node = Nodes[i];
                List<DataWrapper> options = node.EditorOptionDatas;

                if (node.type != NodeType.Dialogue && node.type != NodeType.Choice) continue;

                scanned++;

                if (node.type == NodeType.Dialogue && options.Count >= 4)
                {
                    RepairReferences(node.guid, options, 0, options, 1, 1, ref references, ref updated, ref unresolved, ref recorded);
                    RepairReferences(node.guid, options, 2, options, 3, 1, ref references, ref updated, ref unresolved, ref recorded);
                }
                else if (node.type == NodeType.Choice && options.Count >= 3)
                {
                    RepairReferences(node.guid, options, 0, options, 1, 1, ref references, ref updated, ref unresolved, ref recorded);
                    RepairReferences(node.guid, options, 2, node.EditorPortDatas, 0, node.EditorPortDatas.Count, ref references, ref updated, ref unresolved, ref recorded);
                }

                Nodes[i] = node;
            }

            if (updated > 0)
            {
                EditorUtility.SetDirty(SO);
                SetUnsaved();
            }

            return (scanned, references, updated, unresolved);
        }

        private void RepairReferences(string nodeGuid, List<DataWrapper> tablesData, int tableIndex, List<DataWrapper> entriesData, int start, int count, ref int references, ref int updated, ref int unresolved, ref bool recorded)
        {
            List<GenericData> table = tablesData[tableIndex].data;
            string storedName = table[0].ToString();
            System.Guid storedGuid = table[1].GetGuid();
            LocalizationTableCollection collection = collections.ResolveCollection(storedName, storedGuid);

            if (collection == null)
            {
                for (int i = start; i < start + count; i++)
                {
                    List<GenericData> entry = entriesData[i].data;
                    collection = collections.ResolveCollection(null, System.Guid.Empty, entry[1].GetLong(), entry[0].ToString());

                    if (collection != null) break;
                }
            }

            references++;

            if (collection == null)
            {
                if (storedGuid != System.Guid.Empty || !string.IsNullOrEmpty(storedName))
                {
                    unresolved++;
                    localizationIssues.Add($"Table not found: node={nodeGuid}, guid={storedGuid}, name={storedName}");
                }

                return;
            }

            System.Guid currentGuid = collection.TableCollectionNameReference.TableCollectionNameGuid;

            if (storedName != collection.TableCollectionName || storedGuid != currentGuid)
            {
                if (!recorded)
                {
                    Undo.RecordObject(SO, "Synchronize Localization References");
                    recorded = true;
                }

                List<GenericData> replacement = new(table) { [0] = new(collection.TableCollectionName), [1] = new(currentGuid) };
                tablesData[tableIndex] = new(replacement);
                updated++;
            }

            for (int i = start; i < start + count; i++)
            {
                List<GenericData> data = entriesData[i].data;
                string key = data[0].ToString();
                long id = data[1].GetLong();

                if (id == 0 && string.IsNullOrEmpty(key)) continue;

                references++;
                SharedTableData.SharedTableEntry entry = collection.ResolveEntry(id, key);

                if (entry == null)
                {
                    unresolved++;
                    localizationIssues.Add($"Entry not found: node={nodeGuid}, table={collection.TableCollectionName}, id={id}, key={key}");
                    continue;
                }

                if (key == entry.Key && id == entry.Id) continue;

                if (!recorded)
                {
                    Undo.RecordObject(SO, "Synchronize Localization References");
                    recorded = true;
                }

                List<GenericData> replacement = new(data) { [0] = new(entry.Key), [1] = new(entry.Id) };
                entriesData[i] = new(replacement);
                updated++;
            }
        }

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
            languageDropdown.tooltip = "Localization text edits are saved directly to String Tables, independently of graph changes.";
            languageDropdown.style.minWidth = 110;
            languageDropdown.style.marginLeft = 0;
            languageDropdown.style.marginRight = 0;
            languageDropdown.style.marginTop = 0;
            languageDropdown.style.marginBottom = 0;
            languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
            languageBar.Add(label);
            languageBar.Add(languageDropdown);
            languageBar.style.display = toolsCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            topRightBar.Insert(System.Math.Min(1, topRightBar.childCount), languageBar);
        }

        private void BuildTopRightBar()
        {
            topRightBar = new VisualElement();
            topRightBar.style.position = Position.Absolute;
            topRightBar.style.top = 12;
            topRightBar.style.right = 12;
            topRightBar.style.width = Mathf.Clamp(rootVisualElement.layout.width - 24f, 260f, 420f);
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

            toolbarTitle = new Label("Dialogue Tools");
            toolbarTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            toolbarTitle.style.fontSize = 13;
            toolbarTitle.style.color = new Color(0.88f, 0.92f, 1f);
            toolbarTitle.style.marginBottom = 4;

            collapseButton = new Button(ToggleTools) { text = "−", tooltip = "Collapse or expand dialogue tools" };
            collapseButton.style.width = 24;
            collapseButton.style.marginLeft = 6;

            VisualElement heading = new();
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.alignItems = Align.Center;
            toolbarTitle.style.flexGrow = 1;
            heading.Add(toolbarTitle);
            heading.Add(collapseButton);
            topRightBar.Add(heading);

            assetBar = new VisualElement();
            StyleBar(assetBar);
            assetBar.style.marginTop = 6;
            assetField = new ObjectField("Dialogue") { objectType = typeof(DialogueSO), allowSceneObjects = false, value = SO };
            assetField.style.flexGrow = 1;
            assetField.RegisterValueChangedCallback(evt => SO = evt.newValue as DialogueSO);
            assetBar.Add(assetField);
            topRightBar.Add(assetBar);

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

            searchField = new TextField()
            {
                name = "node-search",
                tooltip = "Search by node ID, node type, or a value stored inside a node.",
            };
            searchField.style.flexGrow = 1;
            searchField.style.minWidth = 80;
            searchField.style.marginLeft = 0;
            searchField.style.marginRight = 4;
            searchField.style.marginTop = 0;
            searchField.style.marginBottom = 0;

            searchSuggestions = new ScrollView(ScrollViewMode.Vertical) { name = "node-suggestions" };
            ScrollView suggestions = searchSuggestions;
            suggestions.style.display = DisplayStyle.None;
            suggestions.style.marginTop = 4;
            suggestions.style.maxHeight = 320;
            suggestions.style.minWidth = 0;
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
                if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)
                {
                    MoveSuggestion(evt.keyCode == KeyCode.DownArrow ? 1 : -1);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    HideSuggestions(suggestions);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    SearchNode(searchField, suggestions, evt.shiftKey);
                    evt.StopPropagation();
                }
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

            backButton = new(NavigateBack) { text = "←", tooltip = "Return to previous graph location" };
            forwardButton = new(NavigateForward) { text = "→", tooltip = "Go to next graph location" };
            backButton.style.width = 25;
            forwardButton.style.width = 25;
            inputRow.Add(backButton);
            inputRow.Add(forwardButton);
            RefreshNavigationButtons();
            searchBar.Add(inputRow);

            searchStatus = new Label("Enter: next result · Shift+Enter: previous result");
            searchStatus.style.color = new Color(0.65f, 0.70f, 0.8f);
            searchStatus.style.fontSize = 10;
            searchStatus.style.marginTop = 3;
            searchBar.Add(searchStatus);
            searchBar.Add(suggestions);
            topRightBar.Add(searchBar);

            repairBar = new VisualElement();
            StyleBar(repairBar);
            repairBar.style.justifyContent = Justify.FlexEnd;
            repairBar.style.marginTop = 6;

            refreshButton = new(RefreshEditor)
            {
                text = "↻  Refresh",
                tooltip = "Synchronize all Dialogue and Choice node localization references and rebuild the graph.",
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

            BuildDebugBar();
            // Keep search suggestions above the following toolbar rows.
            searchBar.BringToFront();
        }

        private void ToggleTools()
        {
            toolsCollapsed = !toolsCollapsed;
            assetBar.style.display = toolsCollapsed ? DisplayStyle.None : DisplayStyle.Flex;

            if (languageBar != null) languageBar.style.display = toolsCollapsed ? DisplayStyle.None : DisplayStyle.Flex;

            searchBar.style.display = toolsCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            repairBar.style.display = toolsCollapsed ? DisplayStyle.None : DisplayStyle.Flex;

            if (debugBar != null) debugBar.style.display = toolsCollapsed || !Application.isPlaying ? DisplayStyle.None : DisplayStyle.Flex;

            if (toolsCollapsed) HideSuggestions(searchSuggestions);

            collapseButton.text = toolsCollapsed ? "+" : "−";
        }

        private void OnWindowGeometryChanged(GeometryChangedEvent evt)
        {
            if (topRightBar == null) return;

            topRightBar.style.width = Mathf.Min(420f, Mathf.Max(200f, evt.newRect.width - 24f));
        }

        private void BuildEmptyGraphMessage()
        {
            emptyGraphMessage = new Label("Select a Dialogue asset in the toolbar to edit its graph.");
            emptyGraphMessage.style.position = Position.Absolute;
            emptyGraphMessage.style.left = 20;
            emptyGraphMessage.style.top = 20;
            emptyGraphMessage.style.fontSize = 13;
            emptyGraphMessage.style.color = new Color(0.7f, 0.75f, 0.82f);
            emptyGraphMessage.pickingMode = PickingMode.Ignore;
            rootVisualElement.Add(emptyGraphMessage);
            UpdateEmptyGraphMessage();
        }

        private void UpdateEmptyGraphMessage()
        {
            if (emptyGraphMessage != null) emptyGraphMessage.style.display = SO == null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildDebugBar()
        {
            debugBar = new VisualElement();
            debugBar.style.flexDirection = FlexDirection.Column;
            debugBar.style.marginTop = 8;
            debugBar.style.display = DisplayStyle.None;

            Label title = new("Play Mode · Runner Debug");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            debugBar.Add(title);

            debugRunnerField = new ObjectField("Runner") { objectType = typeof(DialogueRunner), allowSceneObjects = true };
            debugRunnerField.RegisterValueChangedCallback(evt =>
            {
                debugRunner = evt.newValue as DialogueRunner;

                if (debugRunner != null && debugRunner.Dialogue != null && debugRunner.Dialogue != SO) SO = debugRunner.Dialogue;

                lastDebugFollowGuid = null;
                nextGraphDiffCheck = 0;
                RefreshDebugState();
            });
            debugBar.Add(debugRunnerField);

            debugFlowField = new PopupField<string>("Flow", new List<string> { "(no active flow)" }, 0);
            debugFlowField.RegisterValueChangedCallback(_ =>
            {
                lastDebugFollowGuid = null;
                RefreshDebugState();
            });
            debugBar.Add(debugFlowField);

            debugStatus = new Label("Run the game to inspect a flow.");
            debugStatus.style.whiteSpace = WhiteSpace.Normal;
            debugStatus.style.color = new Color(0.72f, 0.84f, 0.95f);
            debugBar.Add(debugStatus);

            debugApplyStatus = new Label("Runtime graph status unavailable.");
            debugApplyStatus.style.whiteSpace = WhiteSpace.Normal;
            debugApplyStatus.style.fontSize = 11;
            debugApplyStatus.style.marginTop = 4;
            debugBar.Add(debugApplyStatus);

            debugApplyButton = new Button(ApplyDebugGraphChanges)
            {
                text = "Apply graph changes to Runner",
                tooltip = "Pause all flows, wait for active callbacks, and rebuild this Runner's runtime graph. Flows remain paused until explicitly resumed."
            };
            debugBar.Add(debugApplyButton);

            debugFollow = new Toggle("Follow active node") { value = true };
            debugFollow.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue) lastDebugFollowGuid = null;
            });
            debugBar.Add(debugFollow);

            VisualElement jumpRow = new();
            StyleBar(jumpRow);
            debugKeyField = new TextField("New key") { value = "main", tooltip = "Flow key used when no flow is active." };
            debugKeyField.style.flexGrow = 1;
            Button jumpButton = new(() => DebugJumpToNode(graph.FirstSelectedGuid()))
            {
                text = "Play / Jump selected",
                tooltip = "Start or jump to the selected node. If the graph has unapplied edits, confirm applying them first; other flows remain paused."
            };
            jumpRow.Add(debugKeyField);
            jumpRow.Add(jumpButton);
            debugBar.Add(jumpRow);
            topRightBar.Add(debugBar);
        }

        public bool CanDebugJump => Application.isPlaying && debugRunner != null && SO != null &&
            debugRunner.Dialogue == SO && !debugRunner.IsApplyingGraphChanges && !debugJumpInProgress;

        private async void ApplyDebugGraphChanges()
        {
            if (!CanDebugJump || debugRunner == null) return;

            DialogueRunner runner = debugRunner;
            debugJumpInProgress = true;
            debugApplyButton.SetEnabled(false);
            debugApplyStatus.text = "Pausing flows and rebuilding runtime graph...";

            try
            {
                bool applied = await runner.ApplyGraphChangesAsync();

                if (applied && runner != null)
                {
                    nextGraphDiffCheck = 0;
                    pendingGraphChanges = false;
                    lastDebugFollowGuid = null;
                    RefreshDebugState();
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, runner);

                if (this != null) debugApplyStatus.text = "Graph apply failed. Check the Console.";
            }
            finally
            {
                debugJumpInProgress = false;

                if (debugApplyButton != null)
                {
                    nextGraphDiffCheck = 0;
                    RefreshDebugState();
                }
            }
        }

        public async void DebugJumpToNode(string guid)
        {
            if (!CanDebugJump) return;

            if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(GetNode(guid).guid))
            {
                debugStatus.text = "Select an existing graph node to test.";
                return;
            }

            DialogueRunner runner = debugRunner;
            DialogueSO dialogue = SO;
            string selectedKey = debugFlowField.value;
            List<DialogueFlowState> flows = new();

            runner.GetFlowStates(flows);
            bool selectedFlowExists = false;

            for (int i = 0; i < flows.Count; i++)
            {
                if (flows[i].Key != selectedKey) continue;

                selectedFlowExists = true;
                break;
            }

            string targetKey = selectedFlowExists ? selectedKey : debugKeyField.value.Trim();

            if (string.IsNullOrEmpty(targetKey))
            {
                debugStatus.text = "Enter a flow key to start.";
                return;
            }

            bool needsApply = runner.HasPendingGraphChanges;

            if (needsApply)
            {
                string nextFlowAction = selectedFlowExists
                    ? $"Flow '{targetKey}' will restart at the selected node."
                    : $"A new flow with key '{targetKey}' will start at the selected node.";
                bool confirmed = EditorUtility.DisplayDialog("Apply graph changes and jump?", "This node belongs to a graph with unapplied edits.\n\n" + "Applying will pause every flow on this Runner and wait for its active callbacks to finish. " + "Other flows will remain paused.\n\n" + nextFlowAction, "Apply and Jump", "Cancel");

                if (!confirmed) return;
            }

            debugJumpInProgress = true;

            try
            {
                if (needsApply)
                {
                    debugStatus.text = "Applying graph changes before jumping...";
                    debugApplyButton.SetEnabled(false);
                    bool applied = await runner.ApplyGraphChangesAsync();

                    if (!applied)
                    {
                        if (this != null) EditorUtility.DisplayDialog("Graph not applied", "The Runner could not apply the graph changes. No jump was performed.", "OK");

                        return;
                    }
                }

                if (this == null || runner == null || !Application.isPlaying || SO != dialogue || debugRunner != runner || runner.Dialogue != dialogue) return;

                if (runner.IsApplyingGraphChanges || runner.HasPendingGraphChanges)
                {
                    EditorUtility.DisplayDialog("Graph changed again", "The graph changed while it was being applied. No jump was performed. Apply the latest changes and try again.", "OK");
                    return;
                }

                if (!runner.HasRuntimeNode(guid) || string.IsNullOrEmpty(GetNode(guid).guid))
                {
                    EditorUtility.DisplayDialog("Node unavailable", "The selected node is no longer available in both the graph and the Runner. No jump was performed.", "OK");
                    return;
                }

                flows.Clear();
                runner.GetFlowStates(flows);
                bool flowExists = false;

                for (int i = 0; i < flows.Count; i++)
                {
                    if (flows[i].Key != targetKey) continue;

                    flowExists = true;
                    break;
                }

                if (flowExists)
                {
                    bool jumped = await runner.RequestJumpNodeAsync(targetKey, guid);

                    if (!jumped)
                    {
                        if (this != null) debugStatus.text = "Jump was canceled because the flow changed.";

                        return;
                    }
                }
                else runner.StartDialogue(targetKey, guid);
                lastDebugFollowGuid = null;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, runner);

                if (this != null) EditorUtility.DisplayDialog("Graph test failed", "The graph apply or test request failed. Check the Console for details.\n\n" + exception.Message, "OK");
            }
            finally
            {
                if (this != null)
                {
                    debugJumpInProgress = false;
                    nextGraphDiffCheck = 0;
                    RefreshDebugState();
                }
            }
        }

        private void Update()
        {
            bool playing = Application.isPlaying;

            if (playing != wasPlaying)
            {
                wasPlaying = playing;
                nextDebugRefresh = 0;
                nextRunnerLookup = 0;
                nextGraphDiffCheck = 0;
                pendingGraphChanges = false;
                lastDebugFollowGuid = null;

                if (!playing)
                {
                    debugRunner = null;
                    debugRunnerField?.SetValueWithoutNotify(null);
                    graph?.SetDebugNode(null);
                }
            }

            if (debugBar != null) debugBar.style.display = playing && !toolsCollapsed ? DisplayStyle.Flex : DisplayStyle.None;

            if (!playing || EditorApplication.timeSinceStartup < nextDebugRefresh) return;

            nextDebugRefresh = EditorApplication.timeSinceStartup + 0.2;
            RefreshDebugState();
        }

        private void RefreshDebugState()
        {
            if (debugBar == null || !Application.isPlaying || debugJumpInProgress) return;

            if (debugRunner == null && SO != null && EditorApplication.timeSinceStartup >= nextRunnerLookup)
            {
                nextRunnerLookup = EditorApplication.timeSinceStartup + 1;
                DialogueRunner[] runners = FindObjectsByType<DialogueRunner>();

                for (int i = 0; i < runners.Length; i++)
                {
                    if (runners[i].Dialogue != SO) continue;

                    debugRunner = runners[i];
                    nextGraphDiffCheck = 0;
                    debugRunnerField.SetValueWithoutNotify(debugRunner);
                    break;
                }
            }

            debugFlows.Clear();

            if (debugRunner != null) debugRunner.GetFlowStates(debugFlows);

            string selectedKey = debugFlowField.value;
            List<string> choices = new();

            for (int i = 0; i < debugFlows.Count; i++) choices.Add(debugFlows[i].Key);

            if (choices.Count == 0) choices.Add("(no active flow)");

            bool different = choices.Count != debugFlowField.choices.Count;

            if (!different)
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    if (choices[i] == debugFlowField.choices[i]) continue;

                    different = true;
                    break;
                }
            }

            if (different) debugFlowField.choices = choices;

            if (!choices.Contains(selectedKey)) selectedKey = choices[0];

            debugFlowField.SetValueWithoutNotify(selectedKey);

            if (debugRunner == null)
            {
                debugApplyStatus.text = "Select a Runner to check graph changes.";
                debugApplyButton.SetEnabled(false);
                debugStatus.text = "Select a scene DialogueRunner.";
                graph.SetDebugNode(null);
                return;
            }

            if (SO != debugRunner.Dialogue)
            {
                debugStatus.text = "The selected Runner uses a different Dialogue asset.";
                debugApplyStatus.text = "Select the Runner's Dialogue asset to apply changes.";
                debugApplyButton.SetEnabled(false);
                graph.SetDebugNode(null);
                return;
            }

            if (EditorApplication.timeSinceStartup >= nextGraphDiffCheck)
            {
                nextGraphDiffCheck = EditorApplication.timeSinceStartup + 1;
                pendingGraphChanges = debugRunner.HasPendingGraphChanges;
            }

            debugApplyStatus.text = debugRunner.IsApplyingGraphChanges
                ? "Pausing flows and rebuilding runtime graph..."
                : pendingGraphChanges
                    ? "Graph changes are not applied. The Runner continues using its previous snapshot."
                    : "Runtime snapshot matches the editor. Applying changes pauses active flows.";
            debugApplyButton.SetEnabled(!debugRunner.IsApplyingGraphChanges && pendingGraphChanges);

            for (int i = 0; i < debugFlows.Count; i++)
            {
                DialogueFlowState flow = debugFlows[i];

                if (flow.Key != selectedKey) continue;

                NodeData node = GetNode(flow.NodeGuid);
                debugStatus.text = debugRunner.TryGetRuntimeNodeType(flow.NodeGuid, out NodeType runtimeType)
                    ? $"{flow.State} · {runtimeType} (runtime snapshot) · {flow.NodeGuid}"
                    : $"{flow.State} · Unknown runtime node · {flow.NodeGuid}";
                graph.SetDebugNode(flow.NodeGuid);

                if (debugFollow.value && !toolsCollapsed && flow.NodeGuid != lastDebugFollowGuid && !string.IsNullOrEmpty(node.guid))
                {
                    lastDebugFollowGuid = flow.NodeGuid;
                    graph.FocusNode(flow.NodeGuid);
                }

                return;
            }

            graph.SetDebugNode(null);
            debugStatus.text = "No active flow. Select a node and press Play / Jump selected.";
        }

        private static void StyleBar(VisualElement bar)
        {
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
        }

        private void RefreshEditor()
        {
            string source = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            Debug.Log($"Dialogue Refresh clicked: source={source}", SO);
            DialogueVariableSchemaUtility.Invalidate();
            RefreshLocalization();
            (int nodes, int references, int updated, int unresolved) result = lastLocalizationSync;
            refreshButton.text = $"↻ Updated {result.updated} / Missing {result.unresolved}";
            Debug.Log($"Dialogue localization refresh: nodes={result.nodes}, references={result.references}, updated={result.updated}, unresolved={result.unresolved}", SO);

            foreach (string issue in localizationIssues) Debug.LogWarning(issue, SO);
        }

        private const int MAX_SUGGESTIONS = 10;

        private void RefreshSuggestions(TextField field, VisualElement container, string rawQuery)
        {
            container.Clear();
            suggestionResults.Clear();
            suggestionItems.Clear();
            selectedSuggestion = -1;
            string query = rawQuery?.Trim();

            if (SO == null || string.IsNullOrEmpty(query))
            {
                matchingQuery = null;
                searchMatches.Clear();
                matchingIndex = -1;

                if (searchStatus != null) searchStatus.text = "Enter: next result · Shift+Enter: previous result";

                HideSuggestions(container);
                return;
            }

            EnsureMatches(query);
            int shown = 0;

            foreach (NodeSearchResult result in searchMatches)
            {
                VisualElement item = MakeSuggestion(field, container, result);
                suggestionResults.Add(result);
                suggestionItems.Add(item);
                container.Add(item);

                if (++shown >= MAX_SUGGESTIONS) break;
            }

            container.style.display = shown > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

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
                matchingIndex = FindMatchIndex(result.Node.guid);
                UpdateSearchStatus();
                NavigateToSearchResult(result.Node.guid, container);
                evt.StopPropagation();
            });

            return item;
        }

        private void HideSuggestions(VisualElement container)
        {
            container.Clear();
            container.style.display = DisplayStyle.None;
            suggestionResults.Clear();
            suggestionItems.Clear();
            selectedSuggestion = -1;
        }

        private void MoveSuggestion(int delta)
        {
            if (suggestionResults.Count == 0 || searchSuggestions.style.display.value == DisplayStyle.None) return;

            selectedSuggestion = selectedSuggestion < 0 ? (delta > 0 ? 0 : suggestionResults.Count - 1)
                : (selectedSuggestion + delta + suggestionResults.Count) % suggestionResults.Count;

            for (int i = 0; i < suggestionItems.Count; i++)
            {
                if (i == selectedSuggestion) suggestionItems[i].style.backgroundColor = new Color(0.18f, 0.28f, 0.44f, 0.75f);
                else suggestionItems[i].style.backgroundColor = StyleKeyword.Null;
            }

            searchSuggestions.ScrollTo(suggestionItems[selectedSuggestion]);
        }

        private void NavigateToSearchResult(string guid, VisualElement suggestions)
        {
            HideSuggestions(suggestions);
            FocusNode(guid, true);
            searchField?.Focus();
        }

        private void SearchNode(TextField field, VisualElement suggestions, bool reverse = false)
        {
            if (SO == null) return;

            string query = field.value?.Trim();

            if (string.IsNullOrEmpty(query)) return;

            EnsureMatches(query);
            NodeData exact = GetNode(query);
            string guid;

            if (!string.IsNullOrEmpty(exact.guid)) guid = exact.guid;
            else if (selectedSuggestion >= 0 && selectedSuggestion < suggestionResults.Count)
            {
                guid = suggestionResults[selectedSuggestion].Node.guid;
                matchingIndex = FindMatchIndex(guid);
            }
            else if (searchMatches.Count > 0)
            {
                matchingIndex = matchingIndex < 0 ? (reverse ? searchMatches.Count - 1 : 0)
                    : (matchingIndex + (reverse ? -1 : 1) + searchMatches.Count) % searchMatches.Count;
                guid = searchMatches[matchingIndex].Node.guid;
            }
            else guid = null;

            if (guid == null)
            {
                FlashInvalid(field);
                return;
            }

            UpdateSearchStatus();
            NavigateToSearchResult(guid, suggestions);
        }

        private void EnsureMatches(string query)
        {
            if (!searchIndexDirty && matchingQuery == query) return;

            EnsureSearchIndex();
            searchMatches.Clear();

            foreach (NodeSearchDocument document in searchIndex)
            {
                if (TryMatchNode(document, query, out NodeSearchResult result)) searchMatches.Add(result);
            }

            matchingQuery = query;
            matchingIndex = -1;
            UpdateSearchStatus();
        }

        private int FindMatchIndex(string guid)
        {
            for (int i = 0; i < searchMatches.Count; i++)
            {
                if (searchMatches[i].Node.guid == guid) return i;
            }

            return -1;
        }

        private void UpdateSearchStatus()
        {
            if (searchStatus == null) return;

            searchStatus.text = searchMatches.Count == 0 ? "No matches"
                : matchingIndex < 0 ? $"{searchMatches.Count} matches · Enter: next · Shift+Enter: previous"
                : $"Result {matchingIndex + 1} / {searchMatches.Count} · Enter: next · Shift+Enter: previous";
        }

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

            if (collections == null || tableData == null || tableData.Count < 2 || entryData == null || entryData.Count < 2) return;

            LocalizationTableCollection collection = collections.ResolveCollection(tableData[0].ToString(), tableData[1].GetGuid(), entryData[1].GetLong(), entryData[0].ToString());

            if (collection == null) return;

            string text = new EntryData(entryData[1].GetLong(), entryData[0].ToString(), collection.Tables).GetText(Language);

            if (!string.IsNullOrEmpty(text)) target.Add(text);
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

        private static void FlashInvalid(TextField field)
        {
            VisualElement input = field.Q("unity-text-input") ?? field;
            input.style.backgroundColor = new Color(0.55f, 0.20f, 0.20f);
            input.schedule.Execute(() => input.style.backgroundColor = StyleKeyword.Null).StartingIn(700);
        }

        private static string FormatLocale(Locale locale) => locale == null ? "" : locale.LocaleName;

        private void OnLanguageChanged(ChangeEvent<Locale> evt)
        {
            if (evt.newValue == null) return;

            Language = evt.newValue.Identifier;
            ResetView();
        }

        public void GetEntriesInto(LocalizationTableCollection collection, List<EntryData> target)
        {
            target.Clear();

            if (collection == null) return;

            collection.SetEntries(target);
        }

        private void OnKeyDownEvent(KeyDownEvent evt)
        {
            if (evt.ctrlKey && evt.keyCode == KeyCode.S)
            {
                SaveChanges();
                evt.StopPropagation();
            }
        }

        private void CheckSave()
        {
            if (!hasUnsavedChanges) return;

            bool save = EditorUtility.DisplayDialog("Unsaved Changes", "There are unsaved dialogue changes. Save before continuing?", "Save", "Don't Save");

            if (save) SaveChanges();
            else DiscardChanges();
        }

        public override void SaveChanges()
        {
            if (SO == null) return;

            if (hasUnsavedChanges) EditorUtility.SetDirty(SO);

            AssetDatabase.SaveAssetIfDirty(SO);
            saveData = JsonUtility.ToJson(SO);
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            if (SO == null) return;

            JsonUtility.FromJsonOverwrite(saveData, SO);
            ResetView();
            base.DiscardChanges();
        }

        public void SetUnsaved(bool searchChanged = true)
        {
            nextGraphDiffCheck = 0;
            hasUnsavedChanges = true;

            if (searchChanged) searchIndexDirty = true;
        }

        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] mods)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                UndoPropertyModification mod = mods[i];
                PropertyModification current = mod.currentValue;
                string path = current.propertyPath;

                if (current.target is SharedTableData)
                {
                    ScheduleLocalizationRefresh();
                    break;
                }

                if (current.target == SO && (path.StartsWith("conditions") || path.StartsWith("events")))
                {
                    RequestRebuild();
                    break;
                }
            }

            return mods;
        }

        private void OnViewTransformChanged(GraphView graphView)
        {
            if (SO == null) return;

            string key = ViewStateKey(SO);

            if (string.IsNullOrEmpty(key)) return;

            IResolvedStyle style = graph.contentViewContainer.resolvedStyle;
            pendingViewKey = key;
            pendingViewState = new ViewState { position = style.translate, scale = style.scale.value };
            viewSave ??= rootVisualElement.schedule.Execute(FlushViewState);
            viewSave.ExecuteLater(VIEW_SAVE_DEBOUNCE_MS);
        }

        private static string ViewStateKey(DialogueSO asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);

            return string.IsNullOrEmpty(path) ? null : "Yang.Dialogue.Editor.View." + AssetDatabase.AssetPathToGUID(path);
        }

        private static ViewState ReadViewState(DialogueSO asset)
        {
            string key = ViewStateKey(asset);

            if (!string.IsNullOrEmpty(key) && EditorPrefs.HasKey(key))
            {
                ViewState state = JsonUtility.FromJson<ViewState>(EditorPrefs.GetString(key));

                if (state != null && state.scale.x > 0f && state.scale.y > 0f) return state;
            }

            return new ViewState { position = asset.EditorPosition, scale = asset.EditorScale };
        }

        private void FlushViewState()
        {
            if (string.IsNullOrEmpty(pendingViewKey) || pendingViewState == null) return;

            EditorPrefs.SetString(pendingViewKey, JsonUtility.ToJson(pendingViewState));
            pendingViewKey = null;
            pendingViewState = null;
        }

        private NavigationState CaptureNavigation()
        {
            IResolvedStyle style = graph.contentViewContainer.resolvedStyle;

            return new NavigationState(style.translate, style.scale.value, graph.FirstSelectedGuid());
        }

        private void AddNavigation(List<NavigationState> history, NavigationState state)
        {
            if (history.Count == NAVIGATION_LIMIT) history.RemoveAt(0);

            history.Add(state);
        }

        private void RefreshNavigationButtons()
        {
            if (backButton != null) backButton.SetEnabled(backHistory.Count > 0);

            if (forwardButton != null) forwardButton.SetEnabled(forwardHistory.Count > 0);
        }

        private void NavigateBack()
        {
            if (backHistory.Count == 0) return;

            AddNavigation(forwardHistory, CaptureNavigation());
            NavigationState previous = backHistory[backHistory.Count - 1];
            backHistory.RemoveAt(backHistory.Count - 1);
            RestoreNavigation(previous);
        }

        private void NavigateForward()
        {
            if (forwardHistory.Count == 0) return;

            AddNavigation(backHistory, CaptureNavigation());
            NavigationState next = forwardHistory[forwardHistory.Count - 1];
            forwardHistory.RemoveAt(forwardHistory.Count - 1);
            RestoreNavigation(next);
        }

        public void OnNodeGuidChanged(string previous, string current)
        {
            graph.OnNodeGuidChanged(previous, current);
            UpdateNavigationGuid(backHistory, previous, current);
            UpdateNavigationGuid(forwardHistory, previous, current);
            searchIndexDirty = true;
            matchingQuery = null;
            lastDebugFollowGuid = null;
            nextGraphDiffCheck = 0;
        }

        private static void UpdateNavigationGuid(List<NavigationState> history, string previous, string current)
        {
            for (int i = 0; i < history.Count; i++)
            {
                NavigationState item = history[i];

                if (item.SelectedGuid == previous) history[i] = new NavigationState(item.Position, item.Scale, current);
            }
        }

        private void RestoreNavigation(NavigationState state)
        {
            graph.UpdateViewTransform(state.Position, state.Scale);
            graph.SyncVisibleNodes();
            graph.SelectGuid(state.SelectedGuid);
            RefreshNavigationButtons();
        }

        public string ReadLocalizedText(List<GenericData> tableData, List<GenericData> entryData)
        {
            LocalizationTableCollection collection = collections.ResolveCollection(tableData[0].ToString(), tableData[1].GetGuid(), entryData[1].GetLong(), entryData[0].ToString());

            if (collection == null) return "";

            return new EntryData(entryData[1].GetLong(), entryData[0].ToString(), collection.Tables).GetText(Language);
        }

        public bool CanEditLocalizedText(List<GenericData> tableData, List<GenericData> entryData)
        {
            if (tableData == null || tableData.Count < 2 || entryData == null || entryData.Count < 2) return false;

            LocalizationTableCollection collection = collections.ResolveCollection(tableData[0].ToString(), tableData[1].GetGuid(), entryData[1].GetLong(), entryData[0].ToString());

            if (collection == null) return false;

            foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
            {
                if (reference.asset is not StringTable table || table.LocaleIdentifier != Language) continue;

                return (entryData[1].GetLong() != 0 ? table.GetEntry(entryData[1].GetLong()) : table.GetEntry(entryData[0].ToString())) != null;
            }

            return false;
        }

        public bool EditLocalizedText(List<GenericData> tableData, List<GenericData> entryData, string value)
        {
            LocalizationTableCollection collection = collections.ResolveCollection(tableData[0].ToString(), tableData[1].GetGuid(), entryData[1].GetLong(), entryData[0].ToString());

            if (collection == null) return false;

            foreach (LazyLoadReference<LocalizationTable> reference in collection.Tables)
            {
                if (reference.asset is not StringTable table || table.LocaleIdentifier != Language) continue;

                StringTableEntry entry = entryData[1].GetLong() != 0 ? table.GetEntry(entryData[1].GetLong()) : table.GetEntry(entryData[0].ToString());

                if (entry == null) return false;

                if (entry.Value == value) return true;

                Undo.RecordObject(table, "Edit Dialogue Localization");
                entry.Value = value;
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssetIfDirty(table);
                LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
                ScheduleLocalizationRefresh();
                return true;
            }

            return false;
        }

        #region View
        public void RefreshPortColors() => graph?.RefreshPortColors();

        public void FocusNode(string guid, bool select = false)
        {
            if (SO == null || string.IsNullOrEmpty(GetNode(guid).guid)) return;

            NavigationState from = CaptureNavigation();
            graph.FocusNode(guid, select);
            AddNavigation(backHistory, from);
            forwardHistory.Clear();
            RefreshNavigationButtons();
        }

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

        private void RequestRebuild()
        {
            nextGraphDiffCheck = 0;

            if (hasFocus) ResetView();
            else pendingRebuild = true;
        }

        private void OnFocus()
        {
            if (pendingRebuild) ResetView();
        }

        private void ResetView()
        {
            pendingRebuild = false;
            UpdateEmptyGraphMessage();
            searchIndexDirty = true;
            InvalidateNodeIndices();

            if (SO != null)
            {
                NodeData startNode = SO.EditorStartNode;

                if (string.IsNullOrEmpty(startNode.guid))
                {
                    SO.EditorStartNode = new NodeData(NodeType.Start);
                    EditorUtility.SetDirty(SO);
                }
            }

            rebuildingGraph = true;

            try
            {
                graph.RebuildAll();
            }
            finally
            {
                rebuildingGraph = false;
            }

            graph.MarkDirtyRepaint();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (SO == null || rebuildingGraph) return change;

            bool linksChanged = change.edgesToCreate != null && change.edgesToCreate.Count > 0;

            if (linksChanged) CreateEdge(change.edgesToCreate);

            bool nodesRemoved = false;

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                for (int i = 0; i < change.elementsToRemove.Count; i++)
                {
                    if (change.elementsToRemove[i] is BaseNode node && node.GUID != SO.StartGuid) nodesRemoved = true;
                }

                RemoveNode(change.elementsToRemove);
                graph.RequestSync();
                linksChanged = true;
            }

            if (change.movedElements != null) MoveNode(change.movedElements);

            if (linksChanged) graph.RefreshPortColors();

            EditorUtility.SetDirty(SO);
            SetUnsaved(nodesRemoved);

            return change;
        }
        #endregion

        #region Data
        public void InvalidateNodeIndices() => nodeIndicesDirty = true;

        private void EnsureNodeIndices()
        {
            if (!nodeIndicesDirty) return;

            nodeIndices.Clear();

            for (int i = 0; i < Nodes.Count; i++) nodeIndices.TryAdd(Nodes[i].guid, i);

            nodeIndicesDirty = false;
        }

        public NodeData GetNode(string guid)
        {
            if (SO == null || string.IsNullOrEmpty(guid)) return default;

            if (guid == SO.StartGuid) return SO.EditorStartNode;

            EnsureNodeIndices();

            return nodeIndices.TryGetValue(guid, out int index) ? Nodes[index] : default;
        }

        public void SetNode(string guid, NodeData data)
        {
            if (SO.StartGuid == guid) SO.EditorStartNode = data;
            else
            {
                EnsureNodeIndices();

                if (!nodeIndices.TryGetValue(guid, out int index)) return;

                Nodes[index] = data;

                if (data.guid != guid) InvalidateNodeIndices();
            }
        }

        public void CreateEdge(List<Edge> selectable)
        {
            Undo.RecordObject(SO, "Create Edge");

            foreach (Edge edge in selectable)
            {
                LinkData link = CreateLink(edge);

                if (string.IsNullOrEmpty(link.nodeGuid) || string.IsNullOrEmpty(link.targetGuid)) continue;

                // Virtualized edges may be hidden; enforce one link per output port in the serialized graph.

                for (int i = Links.Count - 1; i >= 0; i--)
                {
                    if (Links[i].nodeGuid == link.nodeGuid && Links[i].outPortIndex == link.outPortIndex) Links.RemoveAt(i);
                }

                Links.Add(link);
            }
        }

        public void RemoveNode<T>(List<T> selectable) where T : ISelectable
        {
            Undo.RecordObject(SO, "Remove Node");
            HashSet<string> removedGuids = new();
            HashSet<LinkData> removedEdges = new();

            foreach (T element in selectable)
            {
                if (element is Edge edge) removedEdges.Add(CreateLink(edge));
                else if (element is BaseNode node && node.GUID != SO.StartGuid) removedGuids.Add(node.GUID);
            }

            if (removedGuids.Count > 0)
            {
                for (int i = Nodes.Count - 1; i >= 0; i--)
                {
                    if (removedGuids.Contains(Nodes[i].guid)) Nodes.RemoveAt(i);
                }

                InvalidateNodeIndices();
            }

            for (int i = Links.Count - 1; i >= 0; i--)
            {
                LinkData link = Links[i];

                if (removedGuids.Contains(link.nodeGuid) || removedGuids.Contains(link.targetGuid) || removedEdges.Contains(link)) Links.RemoveAt(i);
            }
        }

        public void RepairData()
        {
            if (SO == null)
            {
                EditorUtility.DisplayDialog("Dialogue Repair", "There is no Dialogue asset to validate.", "OK");
                return;
            }

            string startGuid = SO.StartGuid;
            HashSet<string> validGuids = new();
            Dictionary<string, int> portCounts = new();
            List<int> invalidNodes = new();
            List<int> invalidLinks = new();

            if (!string.IsNullOrEmpty(startGuid))
            {
                validGuids.Add(startGuid);
                portCounts.Add(startGuid, 1);
            }

            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                NodeData node = Nodes[i];

                if (string.IsNullOrEmpty(node.guid) || node.guid == startGuid || !validGuids.Add(node.guid)) invalidNodes.Add(i);
                else portCounts.Add(node.guid, node.type == NodeType.Choice || node.type == NodeType.Condition ? node.PortDatas.Count : 1);
            }

            HashSet<LinkData> seenLinks = new();
            HashSet<(string, int)> seenPorts = new();

            for (int i = Links.Count - 1; i >= 0; i--)
            {
                LinkData link = Links[i];
                bool invalid = string.IsNullOrEmpty(link.nodeGuid) || string.IsNullOrEmpty(link.targetGuid) ||
                               link.outPortIndex < 0 || !validGuids.Contains(link.nodeGuid) || !validGuids.Contains(link.targetGuid) ||
                               link.outPortIndex >= portCounts[link.nodeGuid] || !seenLinks.Add(link);

                if (!invalid && !seenPorts.Add((link.nodeGuid, link.outPortIndex))) invalid = true;

                if (invalid) invalidLinks.Add(i);
            }

            if (invalidNodes.Count == 0 && invalidLinks.Count == 0)
            {
                EditorUtility.DisplayDialog("Dialogue Repair", "No issues were found.", "OK");
                return;
            }

            Undo.RecordObject(SO, "Repair Dialogue Data");
            // The indices were collected backwards, so removal keeps the retained node/link identical to validation.

            for (int i = 0; i < invalidNodes.Count; i++) Nodes.RemoveAt(invalidNodes[i]);

            for (int i = 0; i < invalidLinks.Count; i++) Links.RemoveAt(invalidLinks[i]);

            InvalidateNodeIndices();
            EditorUtility.SetDirty(SO);
            SetUnsaved();
            graph.RebuildAll();
            EditorUtility.DisplayDialog("Dialogue Repair", $"Repair complete.\n\n• Removed nodes: {invalidNodes.Count}\n• Removed links: {invalidLinks.Count}\n\nSave the changes with Ctrl+S.", "OK");
        }

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
                        NodeData data = GetNode(node.GUID);
                        data.position = node.GraphPosition;
                        SetNode(node.GUID, data);
                    }
                }
            }
        }

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

        #endregion
    }
}
