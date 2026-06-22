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
    public class DialogueEditorWindow : EditorWindow
    {
        private DialogueGraph graph;

        private const long LOCALIZATION_REFRESH_DEBOUNCE_MS = 200;

        private IVisualElementScheduledItem localizationRefresh;

        private VisualElement languageBar;
        private PopupField<Locale> languageDropdown;
        private readonly List<Locale> locales = new();

        private readonly Dictionary<string, List<EntryData>> entryCache = new();
        private readonly Dictionary<object, List<string>> keyCache = new();

        private string saveData;

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

        private void OnEnable()
        {
            graph = new DialogueGraph(this);
            graph.StretchToParentSize();

            rootVisualElement.Add(graph);

            Undo.undoRedoPerformed -= ResetView;
            Undo.undoRedoPerformed += ResetView;

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

            BuildLanguageDropdown();

            SO = SO;
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(graph);

            Undo.undoRedoPerformed -= ResetView;

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
        }

        [MenuItem("Tools/Dialogue")]
        public static DialogueEditorWindow Open() => GetWindow<DialogueEditorWindow>("Dialogue");

        private void OnLocalizationCollectionChanged(LocalizationTableCollection collection) => ScheduleLocalizationRefresh();

        private void OnLocalizationEntryChanged(LocalizationTableCollection collection, SharedTableData.SharedTableEntry entry) => ScheduleLocalizationRefresh();

        private void OnLocaleChanged(Locale locale) => ScheduleLocalizationRefresh();

        private void ScheduleLocalizationRefresh()
        {
            localizationRefresh ??= rootVisualElement.schedule.Execute(RefreshLocalization);

            localizationRefresh.ExecuteLater(LOCALIZATION_REFRESH_DEBOUNCE_MS);
        }

        private void RefreshLocalization()
        {
            BuildLanguageDropdown();

            if (SO == null) return;

            collections = LocalizationEditorSettings.GetStringTableCollections();
            collections.SetTables(Tables);

            ResetView();
        }

        private void BuildLanguageDropdown()
        {
            if (languageBar != null)
            {
                rootVisualElement.Remove(languageBar);

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

            languageBar.style.position = Position.Absolute;
            languageBar.style.top = 8;
            languageBar.style.right = 8;

            languageBar.style.flexDirection = FlexDirection.Row;
            languageBar.style.alignItems = Align.Center;

            languageBar.style.paddingLeft = 6;
            languageBar.style.paddingRight = 6;
            languageBar.style.paddingTop = 3;
            languageBar.style.paddingBottom = 3;

            languageBar.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);

            languageBar.style.borderTopLeftRadius = 4;
            languageBar.style.borderTopRightRadius = 4;
            languageBar.style.borderBottomLeftRadius = 4;
            languageBar.style.borderBottomRightRadius = 4;

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

            rootVisualElement.Add(languageBar);
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

            if (!entryCache.TryGetValue(collection.TableCollectionName, out List<EntryData> cached))
            {
                cached = new List<EntryData>();

                collection.SetEntries(cached);

                entryCache[collection.TableCollectionName] = cached;
            }

            target.AddRange(cached);
        }

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

            bool save = EditorUtility.DisplayDialog(
                "Unsaved Changes",
                "There are unsaved changes. Do you want to save them before quitting?",
                "Save",
                "Don't Save"
            );

            if (save) SaveChanges();
            else DiscardChanges();
        }

        public override void SaveChanges()
        {
            if (SO == null) return;

            AssetDatabase.SaveAssetIfDirty(SO);

            saveData = JsonUtility.ToJson(SO);

            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            if (SO == null) return;

            JsonUtility.FromJsonOverwrite(saveData, SO);

            AssetDatabase.SaveAssetIfDirty(SO);

            base.DiscardChanges();
        }

        public void SetUnsaved() => hasUnsavedChanges = true;

        private UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] mods)
        {
            for (int i = 0; i < mods.Length; i++)
            {
                UndoPropertyModification mod = mods[i];
                PropertyModification current = mod.currentValue;

                string path = current.propertyPath;

                if (current.target == SO && (path.StartsWith("conditions") || path.StartsWith("events")))
                {
                    ResetView();

                    break;
                }
            }

            return mods;
        }

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

        private void ResetView()
        {
            entryCache.Clear();
            keyCache.Clear();

            if (SO != null)
            {
                foreach (Node node in graph.nodes.ToList()) graph.RemoveElement(node);
                foreach (Edge edge in graph.edges.ToList()) graph.RemoveElement(edge);

                NodeData startNode = SO.EditorStartNode;

                if (string.IsNullOrEmpty(startNode.guid))
                {
                    SO.EditorStartNode = new NodeData(NodeType.Start);

                    startNode = SO.EditorStartNode;

                    EditorUtility.SetDirty(SO);
                }

                graph.CreateNode(startNode);

                for (int i = 0; i < Nodes.Count; i++) graph.CreateNode(Nodes[i]);

                for (int i = Links.Count - 1; i >= 0; i--)
                {
                    LinkData link = Links[i];

                    BaseNode fromNode = GetLinkedNode(link.nodeGuid, out NodeData fromData);
                    BaseNode toNode = GetLinkedNode(link.targetGuid, out NodeData toData);

                    if (fromNode != null && toNode != null)
                    {
                        if (fromData.PortDatas.Count > link.outPortIndex)
                        {
                            Port fromPort = fromNode.outputContainer[link.outPortIndex] as Port;
                            Port toPort = toNode.inputContainer[0] as Port;

                            Edge edge = fromPort.ConnectTo(toPort);

                            graph.AddElement(edge);
                        }
                        else Links.Remove(link);
                    }
                }
            }

            graph.ClearSelection();
            graph.MarkDirtyRepaint();
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (SO == null) return change;

            if (change.edgesToCreate != null) CreateEdge(change.edgesToCreate);

            if (change.elementsToRemove != null) RemoveNode(change.elementsToRemove);

            if (change.movedElements != null) MoveNode(change.movedElements);

            EditorUtility.SetDirty(SO);

            SetUnsaved();

            return change;
        }
        #endregion

        #region Data
        public NodeData GetNode(string guid)
        {
            if (guid == SO.StartGuid) return SO.EditorStartNode;

            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].guid == guid) return Nodes[i];
            }

            return default;
        }

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

        public void CreateEdge(List<Edge> selectable)
        {
            Undo.RecordObject(SO, "Create Edge");

            foreach (Edge edge in selectable)
            {
                LinkData link = CreateLink(edge);

                if (!Links.Contains(link)) Links.Add(link);
            }
        }

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
                }
            }
        }

        private void MoveNode<T>(List<T> selectable) where T : ISelectable
        {
            Undo.RecordObject(SO, "Move Node");

            foreach (T element in selectable)
            {
                if (element is BaseNode node)
                {
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