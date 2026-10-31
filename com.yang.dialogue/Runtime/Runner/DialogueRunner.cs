using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [Header("Dialogue")]
        [SerializeField] private DialogueSO so;

        [Header("Views")]
        [SerializeField] private List<DialogueViewBase> viewBases = new();

        private DialogueSaveData saveData;
        private bool applyingGraphChanges;
        private int runtimeContentHash;
        private int graphGeneration;
        private Task loadCompletion = Task.CompletedTask;

        private readonly List<IDialogueView> views = new();
        private readonly RunnerNode runnerNode = new();
        private readonly RunnerEvent runnerEvent = new();
        private readonly RunnerTrigger runnerTrigger = new();
        private readonly Dictionary<string, RunnerToken> tokens = new();

        public IReadOnlyList<IDialogueView> Views => views;
        public DialogueSO Dialogue => so;
        public bool IsApplyingGraphChanges => applyingGraphChanges;
        public bool HasPendingGraphChanges => so != null && runtimeContentHash != so.GetRuntimeContentHash();
        public bool HasRuntimeNode(string guid) => runnerNode.CheckNode(guid);
        public bool TryGetRuntimeNodeType(string guid, out NodeType type) => runnerNode.TryGetNodeType(guid, out type);
        public HashSet<string> UnsaveKeys { get; private set; } = new();

        private void Awake() => Init();

        private void OnDestroy() => StopAllDialogue();

        public void GetFlowStates(List<DialogueFlowState> results)
        {
            results.Clear();

            foreach (KeyValuePair<string, RunnerToken> pair in tokens)
            {
                RunnerToken token = pair.Value;
                results.Add(new DialogueFlowState(pair.Key, token.TargetNode, token.State));
            }
        }

        public void SetDialogue(DialogueSO so)
        {
            if (so == null || applyingGraphChanges || !loadCompletion.IsCompleted) return;

            bool isRunning = false;

            foreach (RunnerToken token in tokens.Values)
            {
                if (token.State == TokenState.Running || token.IsExecuting)
                {
                    isRunning = true;

                    break;
                }
            }

            if (isRunning) return;

            StopAllDialogue();

            this.so = so;

            runnerNode.SetDatas(so);
            runtimeContentHash = so.GetRuntimeContentHash();
            graphGeneration++;
        }

        // Reapply only after all old node callbacks have completed. Existing flows remain paused.
        public async Awaitable<bool> ApplyGraphChangesAsync()
        {
            if (so == null || applyingGraphChanges) return false;
            applyingGraphChanges = true;
            graphGeneration++;

            try
            {
                if (!loadCompletion.IsCompleted)
                    await loadCompletion;
                if (this == null) return false;
                List<RunnerToken> currentTokens = new(tokens.Values);

                for (int i = 0; i < currentTokens.Count; i++)
                {
                    RunnerToken token = currentTokens[i];
                    token.JumpRevision++;
                    if (token.State == TokenState.Running)
                        token.SetState(TokenState.Paused);
                }

                List<Task> activeExecutions = new();

                foreach (RunnerToken token in currentTokens)
                {
                    if (token.IsExecuting)
                        activeExecutions.Add(token.ExecutionCompletion.Task);
                }

                if (activeExecutions.Count > 0)
                    await Task.WhenAll(activeExecutions);
                if (this == null) return false;
                runnerNode.SetDatas(so);
                runtimeContentHash = so.GetRuntimeContentHash();
                int resetCount = 0;

                foreach (RunnerToken token in tokens.Values)
                {
                    token.ClearChoiceCaches();

                    if (!runnerNode.CheckNode(token.TargetNode))
                    {
                        token.TargetNode = so.StartGuid;
                        resetCount++;
                    }

                    token.NodeIndex = runnerNode.GetNodeIndex(token.TargetNode);
                }

                if (resetCount > 0) Debug.LogWarning($"Dialogue graph reapplied: {resetCount} flow(s) reset to Start because their nodes were removed.", this);
                return true;
            }
            finally
            {
                applyingGraphChanges = false;
            }
        }

        public void StartDialogue(string key, string nodeName = "", IReadOnlyList<IDialogueView> views = null) => _ = StartDialogueAsync(key, nodeName, views);

        public async Awaitable StartDialogueAsync(string key, string nodeName = "", IReadOnlyList<IDialogueView> views = null)
        {
            if (so == null || applyingGraphChanges) return;
            int generation = graphGeneration;
            RunnerToken requestedToken = null;
            int request = 0;

            if (tokens.TryGetValue(key, out requestedToken))
            {
                if (requestedToken.State == TokenState.Running) return;
                request = ++requestedToken.JumpRevision;
            }

            Task pendingLoad = loadCompletion;

            if (!pendingLoad.IsCompleted)
            {
                await pendingLoad;
                if (applyingGraphChanges || generation != graphGeneration) return;

                if (requestedToken != null)
                {
                    if (!tokens.TryGetValue(key, out RunnerToken pendingToken) || !ReferenceEquals(pendingToken, requestedToken) || requestedToken.JumpRevision != request)
                        return;
                }
                else if (tokens.ContainsKey(key)) return;
            }

            if (tokens.TryGetValue(key, out RunnerToken token))
            {
                if (token.State == TokenState.Running) return;
                if (requestedToken == null)
                    request = ++token.JumpRevision;

                if (token.IsExecuting)
                {
                    await token.ExecutionCompletion.Task;

                    if (applyingGraphChanges || graphGeneration != generation || token.JumpRevision != request) return;
                    if (!tokens.TryGetValue(key, out RunnerToken current) || !ReferenceEquals(current, token) || token.State != TokenState.Paused)
                        return;
                }

                if (runnerNode.CheckNode(nodeName))
                    token.TargetNode = nodeName;
                else if (string.IsNullOrEmpty(token.TargetNode))
                    token.TargetNode = so.StartGuid;

                token.NodeIndex = runnerNode.GetNodeIndex(token.TargetNode);
                token.SetView(views ?? (token.Views.Count > 0 ? token.Views : Views));
                token.SetState(TokenState.Running);
            }
            else
            {
                token = new RunnerToken(views ?? Views) { TargetNode = runnerNode.CheckNode(nodeName) ? nodeName : so.StartGuid };
                token.NodeIndex = runnerNode.GetNodeIndex(token.TargetNode);
                tokens.Add(key, token);
            }

            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            token.ExecutionCompletion = completion;
            token.IsExecuting = true;

            try
            {
                while (token.State == TokenState.Running)
                {
                    token.RefreshView();

                    int portIndex = await runnerNode.NextNode(token);

                    if (token.State != TokenState.Running) break;

                    if (token.NodeIndex >= 0 && runnerNode.TryGetLink(token.NodeIndex, portIndex, out int targetIndex))
                    {
                        token.NodeIndex = targetIndex;
                        token.TargetNode = runnerNode.GetNodeGuid(targetIndex);

                        continue;
                    }

                    token.SetState(TokenState.Ended);
                }
            }
            catch (OperationCanceledException) when (token.State != TokenState.Running)
            {
            }
            catch
            {
                token.SetState(TokenState.Stopped);

                throw;
            }
            finally
            {
                token.IsExecuting = false;

                try
                {
                    CompleteDialogue(key, token);
                }
                finally
                {
                    completion.TrySetResult(true);
                }
            }
        }

        public bool IsRunning(string key) => tokens.TryGetValue(key, out RunnerToken token) && token.State == TokenState.Running;

        public void StopDialogue(string key)
        {
            if (!tokens.TryGetValue(key, out RunnerToken token)) return;

            token.JumpRevision++;
            token.SetState(TokenState.Stopped);

            if (!token.IsExecuting)
                CompleteDialogue(key, token);
        }

        public void PauseDialogue(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token))
            {
                token.JumpRevision++;
                token.SetState(TokenState.Paused);
            }
        }

        public void StopAllDialogue()
        {
            foreach (string key in new List<string>(tokens.Keys))
                StopDialogue(key);
        }

        public void PauseAllDialogue()
        {
            foreach (string key in new List<string>(tokens.Keys))
                PauseDialogue(key);
        }

        public void JumpNode(string key, string nodeName) => _ = RequestJumpNodeAsync(key, nodeName);

        // Completes when the new flow is started, not when the dialogue itself ends.
        public async Awaitable<bool> RequestJumpNodeAsync(string key, string nodeName)
        {
            RunnerToken token = await BeginJumpAsync(key, nodeName);
            return token != null;
        }

        // Await the newly started dialogue to completion, as with StartDialogueAsync.
        public async Awaitable JumpNodeAsync(string key, string nodeName)
        {
            RunnerToken token = await BeginJumpAsync(key, nodeName);
            if (token != null && token.IsExecuting)
                await token.ExecutionCompletion.Task;
        }

        #region View
        public bool AddView(IDialogueView view)
        {
            if (view == null || views.Contains(view)) return false;

            views.Add(view);

            return true;
        }

        public bool RemoveView(IDialogueView view) => views.Remove(view);

        public void ClearViews() => views.Clear();
        #endregion

        public DialogueSaveData Save()
        {
            if (so == null) return null;

            DialogueSaveData result = new() { triggerValues = new List<RunnerValue>(runnerTrigger.Values) };

            foreach (KeyValuePair<string, RunnerToken> pair in tokens)
            {
                if (UnsaveKeys.Contains(pair.Key)) continue;

                RunnerToken token = pair.Value;
                DialogueFlowData flow = new() { key = pair.Key, nodeGuid = token.TargetNode };

                result.dialogueFlows.Add(flow);

                foreach (IDialogueView view in token.Views)
                {
                    flow.viewIDs.Add(view.ViewID);
                    object data = view.CaptureView(pair.Key);

                    if (data != null) result.viewDatas.Add(new ViewDataEntry { key = pair.Key, viewID = view.ViewID, data = JsonUtility.ToJson(data) });
                }
            }

            saveData = result;

            return result;
        }

        public void Load(DialogueSaveData data)
        {
            if (data == null || applyingGraphChanges) return;

            graphGeneration++;
            StopAllDialogue();
            // Retired executions must finish before a newly loaded flow uses the same Views.
            List<Task> retiredExecutions = new();
            if (!loadCompletion.IsCompleted)
                retiredExecutions.Add(loadCompletion);

            foreach (RunnerToken token in tokens.Values)
            {
                if (!token.IsExecuting) continue;
                token.NotifyStoppedAfterRetire = true;
                retiredExecutions.Add(token.ExecutionCompletion.Task);
            }

            loadCompletion = retiredExecutions.Count > 0 ? Task.WhenAll(retiredExecutions) : Task.CompletedTask;
            tokens.Clear();
            saveData = JsonUtility.FromJson<DialogueSaveData>(JsonUtility.ToJson(data));

            if (saveData.dialogueFlows != null)
            {
                foreach (DialogueFlowData flow in saveData.dialogueFlows)
                {
                    if (string.IsNullOrWhiteSpace(flow.key) || tokens.ContainsKey(flow.key)) continue;

                    RunnerToken token = new() { TargetNode = flow.nodeGuid };

                    if (flow.viewIDs != null && flow.viewIDs.Count > 0)
                    {
                        List<IDialogueView> selected = new();

                        foreach (string id in flow.viewIDs)
                        {
                            foreach (IDialogueView view in views)
                            {
                                if (view.ViewID == id)
                                {
                                    selected.Add(view);
                                    break;
                                }
                            }
                        }

                        token.SetView(selected);
                    }

                    tokens.Add(flow.key, token);
                }
            }

            runnerTrigger.SetDatas(saveData.triggerValues ?? new List<RunnerValue>());
        }

        public TData Get<TData>(IDialogueView view)
        {
            if (saveData?.viewDatas == null) return default;

            foreach (ViewDataEntry entry in saveData.viewDatas)
            {
                if (entry.viewID == view.ViewID) return JsonUtility.FromJson<TData>(entry.data);
            }

            return default;
        }

        public TData Get<TData>(string key, IDialogueView view)
        {
            if (saveData?.viewDatas == null) return default;

            foreach (ViewDataEntry entry in saveData.viewDatas)
            {
                if (entry.key == key && entry.viewID == view.ViewID) return JsonUtility.FromJson<TData>(entry.data);
            }

            return default;
        }

        #region Event
        public void ClearEventCallbacks() => runnerEvent.ClearCallbacks();

        public void EventRegisterCallback(string id, System.Action<RunnerCommand> callback) => runnerEvent.RegisterCallback(id, callback);

        public void EventUnregisterCallback(string id, System.Action<RunnerCommand> callback) => runnerEvent.UnregisterCallback(id, callback);
        #endregion

        #region Trigger
        public void ClearTriggerValues() => runnerTrigger.ClearValues();

        public void ClearTriggerCallbacks() => runnerTrigger.ClearCallbacks();

        public bool ContainsKey(int fieldId) => runnerTrigger.ContainsKey(fieldId);

        public bool RemoveValue(int fieldId) => runnerTrigger.RemoveValue(fieldId);

        public void SetValue(int fieldId, float value) => runnerTrigger.SetValue(fieldId, value);

        public void SetValue(int fieldId, bool value) => runnerTrigger.SetValue(fieldId, value);

        public float GetFloatValue(int fieldId) => runnerTrigger.GetFloatValue(fieldId);

        public bool GetBoolValue(int fieldId) => runnerTrigger.GetBoolValue(fieldId);

        public void TriggerRegisterCallback(System.Action<int> callback) => runnerTrigger.OnAnyValueChanged += callback;

        public void TriggerUnregisterCallback(System.Action<int> callback) => runnerTrigger.OnAnyValueChanged -= callback;

        public void TriggerRegisterCallback(int fieldId, System.Action callback) => runnerTrigger.RegisterCallback(fieldId, callback);

        public void TriggerUnregisterCallback(int fieldId, System.Action callback) => runnerTrigger.UnregisterCallback(fieldId, callback);
        #endregion

        private async Awaitable<RunnerToken> BeginJumpAsync(string key, string nodeName)
        {
            if (applyingGraphChanges || !tokens.TryGetValue(key, out RunnerToken token) || !runnerNode.CheckNode(nodeName))
                return null;
            int generation = graphGeneration;
            int request = ++token.JumpRevision;
            if (!loadCompletion.IsCompleted)
                await loadCompletion;
            if (applyingGraphChanges || generation != graphGeneration || token.JumpRevision != request) return null;
            if (!tokens.TryGetValue(key, out RunnerToken active) || !ReferenceEquals(active, token)) return null;
            if (token.State == TokenState.Running)
                token.SetState(TokenState.Paused);
            if (token.IsExecuting)
                await token.ExecutionCompletion.Task;
            if (applyingGraphChanges || generation != graphGeneration || token.JumpRevision != request || token.State != TokenState.Paused)
                return null;
            if (!tokens.TryGetValue(key, out RunnerToken current) || !ReferenceEquals(current, token)) return null;
            StartDialogue(key, nodeName, token.Views);
            return token;
        }

        private void CompleteDialogue(string key, RunnerToken token)
        {
            if (!tokens.TryGetValue(key, out RunnerToken current) || !ReferenceEquals(current, token))
            {
                if (token.NotifyStoppedAfterRetire)
                {
                    token.NotifyStoppedAfterRetire = false;
                    token.RefreshView();
                    foreach (IDialogueView view in token.Views)
                        view.OnStopped(key);
                }

                return;
            }

            token.RefreshView();

            switch (token.State)
            {
                case TokenState.Paused:
                    foreach (IDialogueView view in token.Views)
                        view.OnPaused(key);
                    break;

                case TokenState.Stopped:
                    tokens.Remove(key);
                    foreach (IDialogueView view in token.Views)
                        view.OnStopped(key);
                    break;

                case TokenState.Ended:
                    tokens.Remove(key);
                    foreach (IDialogueView view in token.Views)
                        view.OnEnded(key);
                    break;
            }
        }

        private void Init()
        {
            runnerNode.Init(runnerEvent, runnerTrigger);
            views.InsertRange(0, viewBases);
            SetDialogue(so);
        }
    }

    public readonly struct DialogueFlowState
    {
        public string Key { get; }
        public string NodeGuid { get; }
        public TokenState State { get; }

        public DialogueFlowState(string key, string nodeGuid, TokenState state)
        {
            Key = key;
            NodeGuid = nodeGuid;
            State = state;
        }
    }

}
