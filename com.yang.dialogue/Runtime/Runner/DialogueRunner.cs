using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueSO so;

        [SerializeField] private List<DialogueViewBase> viewBases = new();

        private DialogueSaveData saveData;

        private readonly List<IDialogueView> views = new();

        public IReadOnlyList<IDialogueView> Views => views;

        public HashSet<string> UnsaveKeys { get; private set; } = new();

        private readonly RunnerNode runnerNode = new();
        private readonly RunnerEvent runnerEvent = new();
        private readonly RunnerTrigger runnerTrigger = new();

        private readonly Dictionary<string, RunnerToken> tokens = new();

        private void Awake() => Init();

        private void Init()
        {
            runnerNode.Init(runnerEvent, runnerTrigger);
            views.InsertRange(0, viewBases);
            SetDialogue(so);
        }

        public void SetDialogue(DialogueSO so)
        {
            if (so == null) return;

            bool isRunning = false;

            foreach (RunnerToken token in tokens.Values)
            {
                if (token.State == TokenState.Running)
                {
                    isRunning = true;

                    break;
                }
            }

            if (isRunning) return;

            StopAllDialogue();

            this.so = so;

            runnerNode.SetDatas(so);
        }

        public void StartDialogue(string key, string nodeName = "", IReadOnlyList<IDialogueView> views = null) => _ = StartDialogueAsync(key, nodeName, views);

        public async Awaitable StartDialogueAsync(string key, string nodeName = "", IReadOnlyList<IDialogueView> views = null)
        {
            if (so == null) return;

            if (tokens.TryGetValue(key, out RunnerToken token))
            {
                if (token.State == TokenState.Running) return;

                if (token.IsExecuting)
                {
                    await token.ExecutionCompletion.Task;

                    if (!tokens.TryGetValue(key, out RunnerToken current) || !ReferenceEquals(current, token) || token.State == TokenState.Running) return;
                }

                if (runnerNode.CheckNode(nodeName)) token.TargetNode = nodeName;
                else if (string.IsNullOrEmpty(token.TargetNode)) token.TargetNode = so.StartGuid;

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

                    int portIndex = await runnerNode.NextNode(token, token);

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
            catch (OperationCanceledException) when (token.State != TokenState.Running) { }
            catch
            {
                token.SetState(TokenState.Stopped);

                throw;
            }
            finally
            {
                token.IsExecuting = false;

                try { CompleteDialogue(key, token); }
                finally { completion.TrySetResult(true); }
            }
        }

        private void CompleteDialogue(string key, RunnerToken token)
        {
            if (!tokens.TryGetValue(key, out RunnerToken current) || !ReferenceEquals(current, token)) return;

            token.RefreshView();

            switch (token.State)
            {
                case TokenState.Paused:
                    foreach (IDialogueView view in token.Views) view.OnPaused(key);
                    break;

                case TokenState.Stopped:
                    tokens.Remove(key);
                    foreach (IDialogueView view in token.Views) view.OnStopped(key);
                    break;

                case TokenState.Ended:
                    tokens.Remove(key);
                    foreach (IDialogueView view in token.Views) view.OnEnded(key);
                    break;
            }
        }

        public bool IsRunning(string key) => tokens.TryGetValue(key, out RunnerToken token) && token.State == TokenState.Running;

        public void StopDialogue(string key)
        {
            if (!tokens.TryGetValue(key, out RunnerToken token)) return;

            token.SetState(TokenState.Stopped);

            if (!token.IsExecuting) CompleteDialogue(key, token);
        }

        public void PauseDialogue(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.SetState(TokenState.Paused);
        }

        public void StopAllDialogue()
        {
            foreach (string key in new List<string>(tokens.Keys)) StopDialogue(key);
        }

        public void PauseAllDialogue()
        {
            foreach (string key in new List<string>(tokens.Keys)) PauseDialogue(key);
        }

        public void JumpNode(string key, string nodeName) => _ = JumpNodeAsync(key, nodeName);

        public async Awaitable JumpNodeAsync(string key, string nodeName)
        {
            if (!tokens.TryGetValue(key, out RunnerToken token) || !runnerNode.CheckNode(nodeName)) return;

            if (token.State == TokenState.Running)
            {
                Task completion = token.ExecutionCompletion.Task;

                token.SetState(TokenState.Paused);

                await completion;
            }

            if (tokens.TryGetValue(key, out RunnerToken current) && ReferenceEquals(current, token) && token.State == TokenState.Paused)
                await StartDialogueAsync(key, nodeName, token.Views);
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
            if (data == null) return;

            StopAllDialogue();

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
                                if (view.ViewID == id) { selected.Add(view); break; }
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
    }
}