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

            bool isStarted = false;

            foreach (RunnerToken token in tokens.Values)
            {
                if (token.IsStarted)
                {
                    isStarted = true;

                    break;
                }
            }

            if (isStarted) return;

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

            string nextNode;

            if (tokens.TryGetValue(key, out RunnerToken token))
            {
                if (!token.IsStarted)
                {
                    if (runnerNode.CheckNode(nodeName)) nextNode = nodeName;
                    else nextNode = token.TargetNode == "" ? so.StartGuid : token.TargetNode;

                    token.Resume(nextNode);
                }
                else return;
            }
            else
            {
                if (runnerNode.CheckNode(nodeName)) nextNode = nodeName;
                else nextNode = so.StartGuid;

                token = new(nextNode);

                tokens.Add(key, token);
            }

            views ??= Views;

            while (true)
            {
                IReadOnlyList<IDialogueView> snapshot = Snapshot(views);

                int portIndex = await runnerNode.NextNode(snapshot, token, token);

                if (!token.IsStarted) break;

                if (token.JumpTarget != "" && runnerNode.CheckNode(token.JumpTarget))
                {
                    token.TargetNode = token.JumpTarget;
                    token.JumpTarget = "";
                }
                else
                {
                    RunnerPort port = new(token.TargetNode, portIndex);

                    if (runnerNode.TryGetLink(port, out string result)) token.TargetNode = result;
                    else
                    {
                        foreach (IDialogueView view in snapshot) await view.OnMessage("", token);

                        break;
                    }
                }
            }

            if (token.IsStarted) tokens.Remove(key);
        }

        /// <summary>
        /// Returns a defensive copy of the view list so iteration is unaffected if views are added or removed mid-flow.
        /// </summary>
        private static IReadOnlyList<IDialogueView> Snapshot(IReadOnlyList<IDialogueView> source)
        {
            IDialogueView[] copy = new IDialogueView[source.Count];

            for (int i = 0; i < source.Count; i++) copy[i] = source[i];

            return copy;
        }

        /// <summary>
        /// Reports whether the conversation with the given key is currently running.
        /// Use it to gate input, e.g. if (!runner.IsStarted("npc")) runner.StartDialogue("npc");
        /// </summary>
        public bool IsStarted(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) return token.IsStarted;

            return false;
        }

        /// <summary>
        /// Pauses the conversation with the given key while preserving its position so it can be resumed later.
        /// Call runner.PauseDialogue("npc") to suspend a flow, e.g. when opening a menu.
        /// </summary>
        public void PauseDialogue(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.Pause();
        }

        /// <summary>
        /// Pauses every active conversation and clears all flows. Use it to fully stop dialogue, e.g. on scene exit.
        /// </summary>
        public void StopAllDialogue()
        {
            foreach (RunnerToken token in tokens.Values) token.Pause();

            tokens.Clear();
        }

        /// <summary>
        /// Queues a jump so the named flow continues at <paramref name="nodeName"/> after its current step.
        /// Call runner.JumpNode("npc", "Ending") to redirect a running conversation.
        /// </summary>
        public void JumpNode(string key, string nodeName)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.JumpTarget = nodeName;
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
        public DialogueWrapper Save()
        {
            if (so == null) return null;

            DialogueWrapper wrapper = new();

            wrapper.SetDatas(tokens, runnerTrigger.Values);

            return wrapper;
        }

        /// <summary>
        /// Restores trigger values immediately and returns the saved flows as (key, nodeId) pairs for the caller
        /// to resume via StartDialogue. The trigger restore is eager so it runs even if the result is ignored.
        /// </summary>
        /// <remarks>Call StopAllDialogue() first if the runner already holds flows so the keys don't collide.</remarks>
        public IEnumerable<KeyValuePair<string, string>> Load(DialogueWrapper wrapper)
        {
            if (wrapper == null) return System.Array.Empty<KeyValuePair<string, string>>();

            runnerTrigger.SetDatas(wrapper.Values);

            List<KeyValuePair<string, string>> flows = new();

            if (so != null)
            {
                IReadOnlyList<string> keys = wrapper.Keys;
                IReadOnlyList<string> names = wrapper.Names;

                for (int i = 0; i < keys.Count; i++) flows.Add(new(keys[i], names[i]));
            }

            return flows;
        }

        /// <summary>
        /// One-shot restore that clears existing flows, restores trigger values, and resumes every saved flow at its node.
        /// Use this instead of Load when you just want to apply a save: runner.LoadAndStart(data);
        /// </summary>
        public void LoadAndStart(DialogueWrapper wrapper, IReadOnlyList<IDialogueView> views = null)
        {
            StopAllDialogue();

            foreach (KeyValuePair<string, string> flow in Load(wrapper))
                StartDialogue(flow.Key, flow.Value, views);
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