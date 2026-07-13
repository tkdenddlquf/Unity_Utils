using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// MonoBehaviour entry point that drives a dialogue graph and dispatches its content to registered views.
    /// Add it to a scene GameObject, assign a DialogueSO, then call StartDialogue("conversationKey") to begin.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueSO so;

        [SerializeField] private List<DialogueViewBase> viewBases = new();

        private DialogueSaveData saveData;

        private readonly List<IDialogueView> views = new();

        /// <summary>
        /// The live, read-only list of views that receive dialogue callbacks. Inspect it to see what is currently wired up.
        /// </summary>
        public IReadOnlyList<IDialogueView> Views => views;

        private readonly RunnerNode runnerNode = new();
        private readonly RunnerEvent runnerEvent = new();
        private readonly RunnerTrigger runnerTrigger = new();

        private readonly Dictionary<string, RunnerToken> tokens = new();

        /// <summary>
        /// Unity lifecycle hook that initializes the runner when the GameObject awakes.
        /// </summary>
        private void Awake() => Init();

        /// <summary>
        /// Wires up the node engine, copies the serialized views into the active list, and loads the assigned DialogueSO.
        /// </summary>
        private void Init()
        {
            runnerNode.Init(runnerEvent, runnerTrigger);

            views.InsertRange(0, viewBases);

            SetDialogue(so);
        }

        /// <summary>
        /// Swaps in a new dialogue graph and clears existing flows. Ignored while any conversation is running.
        /// Call this to change which DialogueSO the runner plays, e.g. runner.SetDialogue(chapterTwoSO).
        /// </summary>
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

            this.so = so;

            tokens.Clear();

            runnerNode.SetDatas(so);
        }

        /// <summary>
        /// Starts (or resumes) a conversation identified by <paramref name="key"/> and walks the graph node by node,
        /// awaiting each view callback until the flow ends. Optionally start at a specific node and route to a custom view set.
        /// Typically called as runner.StartDialogue("npc_blacksmith"); the same key resumes a paused flow.
        /// </summary>
        public async void StartDialogue(string key, string nodeName = "", IReadOnlyList<IDialogueView> views = null)
        {
            if (so == null) return;

            views ??= Views;

            if (tokens.TryGetValue(key, out RunnerToken token))
            {
                if (token.State == TokenState.Running) return;
                else
                {
                    if (runnerNode.CheckNode(nodeName)) token.TargetNode = nodeName;
                    else token.TargetNode = token.TargetNode == "" ? so.StartGuid : token.TargetNode;

                    token.SetView(views);
                    token.SetState(TokenState.Running);
                }
            }
            else
            {
                token = new(views)
                {
                    TargetNode = runnerNode.CheckNode(nodeName) ? nodeName : so.StartGuid
                };

                tokens.Add(key, token);
            }

            token.UsedTask = new();

            while (true)
            {
                token.RefreshView();

                int portIndex = await runnerNode.NextNode(token, token);

                if (token.State != TokenState.Running) break;

                if (token.TargetNode != "" && runnerNode.CheckNode(token.TargetNode))
                {
                    RunnerPort port = new(token.TargetNode, portIndex);

                    if (runnerNode.TryGetLink(port, out string result)) token.TargetNode = result;
                    else
                    {
                        token.SetState(TokenState.Ended);

                        break;
                    }
                }
            }

            token.RefreshView();

            switch (token.State)
            {
                case TokenState.Running:
                    return;

                case TokenState.Paused:
                    foreach (IDialogueView view in token.Views) view.OnPaused();
                    break;

                case TokenState.Stopped:
                    foreach (IDialogueView view in token.Views) view.OnStopped();

                    tokens.Remove(key);
                    return;

                case TokenState.Ended:
                    foreach (IDialogueView view in token.Views) view.OnEnded();

                    tokens.Remove(key);
                    return;
            }

            token.UsedTask.SetResult(true);
        }

        public bool IsRunning(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) return token.State == TokenState.Running;

            return false;
        }

        public void StopDialogue(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.SetState(TokenState.Stopped);
        }

        public void PauseDialogue(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.SetState(TokenState.Paused);
        }

        public void StopAllDialogue()
        {
            foreach (RunnerToken token in tokens.Values) token.SetState(TokenState.Stopped);
        }

        public void PauseAllDialogue()
        {
            foreach (RunnerToken token in tokens.Values) token.SetState(TokenState.Paused);
        }

        /// <summary>
        /// Queues a jump so the named flow continues at <paramref name="nodeName"/> after its current step.
        /// Call runner.JumpNode("npc", "Ending") to redirect a running conversation.
        /// </summary>
        public async void JumpNode(string key, string nodeName)
        {
            if (tokens.TryGetValue(key, out RunnerToken token))
            {
                token.SetState(TokenState.Paused);

                await token.UsedTask.Task;

                StartDialogue(key, nodeName, token.Views);
            }
        }

        #region View
        /// <summary>
        /// Registers a view to receive dialogue callbacks; returns false if it is null or already present.
        /// Call runner.AddView(myView) to hook a custom IDialogueView at runtime.
        /// </summary>
        public bool AddView(IDialogueView view)
        {
            if (view == null || views.Contains(view)) return false;

            views.Add(view);

            return true;
        }

        /// <summary>
        /// Unregisters a view so it no longer receives callbacks; returns true if it was present.
        /// </summary>
        public bool RemoveView(IDialogueView view) => views.Remove(view);

        /// <summary>
        /// Removes all registered views, leaving the runner with no callback targets.
        /// </summary>
        public void ClearViews() => views.Clear();
        #endregion

        /// <summary>
        /// Captures the current flows and trigger values into a serializable wrapper for persistence.
        /// Call var data = runner.Save(); and serialize the result to store progress.
        /// </summary>
        public DialogueSaveData Save()
        {
            if (so == null) return null;

            saveData ??= new();

            foreach (RunnerToken token in tokens.Values)
            {
                IReadOnlyList<IDialogueView> views = token.Views;

                for (int i = 0; i < views.Count; i++)
                {
                    IDialogueView view = views[i];

                    object data = view.CaptureView();

                    if (data == null) continue;

                    ViewDataEntry entry = new()
                    {
                        viewID = view.ViewID,
                        data = JsonUtility.ToJson(data)
                    };

                    saveData.viewDatas.Add(entry);
                }
            }

            saveData.dialogueKeys = new(tokens.Keys);
            saveData.triggerValues = new(runnerTrigger.Values);

            return saveData;
        }

        public void Load(DialogueSaveData saveData)
        {
            this.saveData = saveData;

            List<string> dialogueKeys = saveData.dialogueKeys;

            for (int i = 0; i < dialogueKeys.Count; i++) tokens.Add(dialogueKeys[i], new());

            runnerTrigger.SetDatas(saveData.triggerValues);
        }

        public TData Get<TData>(IDialogueView view)
        {
            if (saveData == null) return default;

            List<ViewDataEntry> viewDatas = saveData.viewDatas;

            for (int i = 0; i < viewDatas.Count; i++)
            {
                ViewDataEntry viewData = viewDatas[i];

                if (viewData.viewID == view.ViewID) return JsonUtility.FromJson<TData>(viewData.data);
            }

            return default;
        }

        #region Event
        /// <summary>
        /// Removes all registered event callbacks from the runner's event system.
        /// </summary>
        public void ClearEventCallbacks() => runnerEvent.ClearCallbacks();

        /// <summary>
        /// Subscribes a callback that fires when the dialogue graph reaches the event node with the given id.
        /// Call runner.EventRegisterCallback("playSound", () => audio.Play());
        /// </summary>
        public void EventRegisterCallback(string id, System.Action callback) => runnerEvent.RegisterCallback(id, callback);

        /// <summary>
        /// Unsubscribes a previously registered event callback for the given id.
        /// </summary>
        public void EventUnregisterCallback(string id, System.Action callback) => runnerEvent.UnregisterCallback(id, callback);
        #endregion

        #region Trigger
        /// <summary>
        /// Clears all stored trigger variable values used by graph conditions.
        /// </summary>
        public void ClearTriggerValues() => runnerTrigger.ClearValues();

        /// <summary>
        /// Removes all callbacks subscribed to trigger value changes.
        /// </summary>
        public void ClearTriggerCallbacks() => runnerTrigger.ClearCallbacks();

        /// <summary>
        /// Returns whether a trigger variable with the given key currently has a value.
        /// </summary>
        public bool ContainsKey(string key) => runnerTrigger.ContainsKey(key);

        /// <summary>
        /// Removes the trigger variable with the given key; returns true if it existed.
        /// </summary>
        public bool RemoveValue(string key) => runnerTrigger.RemoveValue(key);

        /// <summary>
        /// Sets a float trigger variable used by graph conditions, e.g. runner.SetValue("affection", 5f).
        /// </summary>
        public void SetValue(string key, float value) => runnerTrigger.SetValue(key, value);

        /// <summary>
        /// Sets a bool trigger variable used by graph conditions, e.g. runner.SetValue("hasKey", true).
        /// </summary>
        public void SetValue(string key, bool value) => runnerTrigger.SetValue(key, value);

        /// <summary>
        /// Gets the float value of a trigger variable, e.g. float a = runner.GetFloatValue("affection").
        /// </summary>
        public float GetFloatValue(string key) => runnerTrigger.GetFloatValue(key);

        /// <summary>
        /// Gets the bool value of a trigger variable, e.g. bool b = runner.GetBoolValue("hasKey").
        /// </summary>
        public bool GetBoolValue(string key) => runnerTrigger.GetBoolValue(key);

        /// <summary>
        /// Subscribes a callback that fires for any trigger value change, receiving the changed key.
        /// Call runner.TriggerRegisterCallback(key => Refresh(key)) to observe all variables.
        /// </summary>
        public void TriggerRegisterCallback(System.Action<string> callback) => runnerTrigger.OnAnyValueChanged += callback;

        /// <summary>
        /// Unsubscribes a callback previously registered for any trigger value change.
        /// </summary>
        public void TriggerUnregisterCallback(System.Action<string> callback) => runnerTrigger.OnAnyValueChanged -= callback;

        /// <summary>
        /// Subscribes a callback that fires when the specific trigger variable changes.
        /// Call runner.TriggerRegisterCallback("hasKey", () => UpdateDoor());
        /// </summary>
        public void TriggerRegisterCallback(string key, System.Action callback) => runnerTrigger.RegisterCallback(key, callback);

        /// <summary>
        /// Unsubscribes a callback previously registered for the specific trigger variable.
        /// </summary>
        public void TriggerUnregisterCallback(string key, System.Action callback) => runnerTrigger.UnregisterCallback(key, callback);
        #endregion
    }
}