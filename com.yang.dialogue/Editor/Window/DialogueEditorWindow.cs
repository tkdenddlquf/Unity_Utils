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
        private DialogueGraph graph;

        private const long LOCALIZATION_REFRESH_DEBOUNCE_MS = 200;

        private IVisualElementScheduledItem localizationRefresh;

        private VisualElement topRightBar;
        private VisualElement languageBar;
        private VisualElement searchBar;
        private PopupField<Locale> languageDropdown;
        private readonly List<Locale> locales = new();

        private readonly Dictionary<string, List<EntryData>> entryCache = new();
        private readonly Dictionary<object, List<string>> keyCache = new();

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

            topRightBar.Insert(0, languageBar);
        }

        /// <summary>Creates the top-right toolbar stack and the node-search bar.</summary>
        private void BuildTopRightBar()
        {
            topRightBar = new VisualElement();

            topRightBar.style.position = Position.Absolute;
            topRightBar.style.top = 8;
            topRightBar.style.right = 8;

            topRightBar.style.flexDirection = FlexDirection.Column;
            topRightBar.style.alignItems = Align.Stretch;

            topRightBar.style.paddingLeft = 6;
            topRightBar.style.paddingRight = 6;
            topRightBar.style.paddingTop = 3;
            topRightBar.style.paddingBottom = 3;

            topRightBar.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);

            topRightBar.style.borderTopLeftRadius = 4;
            topRightBar.style.borderTopRightRadius = 4;
            topRightBar.style.borderBottomLeftRadius = 4;
            topRightBar.style.borderBottomRightRadius = 4;

            rootVisualElement.Add(topRightBar);

            searchBar = new VisualElement();

            StyleBar(searchBar);

            searchBar.style.flexDirection = FlexDirection.Column;
            searchBar.style.alignItems = Align.Stretch;
            searchBar.style.marginTop = 6;

            VisualElement inputRow = new();

            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems = Align.Center;

            Label searchLabel = new("ID")
            {
                style =
                {
                    marginRight = 6,
                    color = new Color(0.78f, 0.78f, 0.78f),
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };

            TextField searchField = new() { name = "node-search", tooltip = "노드 ID 입력 — 일치하는 항목을 골라 이동" };

            searchField.style.minWidth = 160;
            searchField.style.marginLeft = 0;
            searchField.style.marginRight = 4;
            searchField.style.marginTop = 0;
            searchField.style.marginBottom = 0;

            VisualElement suggestions = new() { name = "node-suggestions" };

            suggestions.style.display = DisplayStyle.None;
            suggestions.style.marginTop = 4;

            searchField.RegisterValueChangedCallback(evt => RefreshSuggestions(searchField, suggestions, evt.newValue));

            searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;

                SearchNode(searchField, suggestions);

                evt.StopPropagation();
            });

            searchField.RegisterCallback<FocusOutEvent>(_ => suggestions.schedule.Execute(() => HideSuggestions(suggestions)).StartingIn(150));

            Button searchButton = new(() => SearchNode(searchField, suggestions)) { text = "이동" };

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

            Button repairButton = new(RepairData)
            {
                text = "데이터 검사 / 복구",
                tooltip = "끊어진 링크·중복·고아 노드 등 그래프 데이터와 뷰의 결함을 검사해 자동으로 정리합니다.",
            };

            repairButton.style.marginLeft = 0;
            repairButton.style.marginRight = 0;
            repairButton.style.marginTop = 0;
            repairButton.style.marginBottom = 0;

            repairBar.Add(repairButton);

            topRightBar.Add(repairBar);
        }

        /// <summary>Lays a toolbar row out horizontally; the shared dark background is applied once on the container.</summary>
        private static void StyleBar(VisualElement bar)
        {
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
        }

        private const int MAX_SUGGESTIONS = 10;

        /// <summary>Rebuilds the autocomplete list from node ids matching the typed text.</summary>
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

            foreach (string id in AllNodeIds())
            {
                if (id.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                container.Add(MakeSuggestion(field, container, id));

                if (++shown >= MAX_SUGGESTIONS) break;
            }

            container.style.display = shown > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>One clickable row in the autocomplete list.</summary>
        private VisualElement MakeSuggestion(TextField field, VisualElement container, string id)
        {
            Label item = new(id);

            item.style.paddingLeft = 6;
            item.style.paddingRight = 6;
            item.style.paddingTop = 2;
            item.style.paddingBottom = 2;
            item.style.color = new Color(0.86f, 0.86f, 0.86f);
            item.style.whiteSpace = WhiteSpace.NoWrap;

            Color hover = new(1f, 1f, 1f, 0.12f);

            item.RegisterCallback<MouseEnterEvent>(_ => item.style.backgroundColor = hover);
            item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = StyleKeyword.Null);

            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                field.SetValueWithoutNotify(id);

                HideSuggestions(container);

                FocusNode(id, true);

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

        /// <summary>Returns the first node id containing the query, or null if none match.</summary>
        private string FirstSuggestion(string query)
        {
            foreach (string id in AllNodeIds())
            {
                if (id.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0) return id;
            }

            return null;
        }

        /// <summary>Returns all node ids including the start node, for search and autocomplete.</summary>
        private List<string> AllNodeIds()
        {
            List<string> ids = new();

            if (SO == null) return ids;

            if (!string.IsNullOrEmpty(SO.StartGuid)) ids.Add(SO.StartGuid);

            for (int i = 0; i < Nodes.Count; i++)
            {
                if (!string.IsNullOrEmpty(Nodes[i].guid)) ids.Add(Nodes[i].guid);
            }

            return ids;
        }

        /// <summary>Briefly tints the search field red to signal no matching node id.</summary>
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

        /// <summary>Fills the target list with the marker's keys, caching per marker.</summary>
        public void GetKeysInto(object marker, List<string> target)
        {
            target.Clear();

            if (marker == null) return;

            if (!keyCache.TryGetValue(marker, out List<string> cached))
            {
                cached = new List<string>();

                KeyConverter.GetKeys(marker, cached);

                keyCache[marker] = cached;
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
        public void SetUnsaved() => hasUnsavedChanges = true;

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

            entryCache.Clear();
            keyCache.Clear();

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
                EditorUtility.DisplayDialog("데이터 복구", "검사할 Dialogue 에셋이 없습니다.", "확인");

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
                EditorUtility.DisplayDialog("데이터 복구", "결함이 발견되지 않았습니다.", "확인");
            }
            else
            {
                EditorUtility.DisplayDialog("데이터 복구", $"복구를 완료했습니다.\n\n• 정리된 노드: {removedNodes}개\n• 정리된 링크: {removedLinks}개\n\n변경 사항을 저장(Ctrl+S)하세요.", "확인");
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