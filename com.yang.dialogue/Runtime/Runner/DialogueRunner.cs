using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueSO so;

        [SerializeField] private List<DialogueViewBase> viewBases = new();

        private readonly List<IDialogueView> views = new();
        public IReadOnlyList<IDialogueView> Views => views;

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

        private static IReadOnlyList<IDialogueView> Snapshot(IReadOnlyList<IDialogueView> source)
        {
            IDialogueView[] copy = new IDialogueView[source.Count];

            for (int i = 0; i < source.Count; i++) copy[i] = source[i];

            return copy;
        }

        public bool IsStarted(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) return token.IsStarted;

            return false;
        }

        public void PauseDialogue(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.Pause();
        }

        public void StopAllDialogue()
        {
            foreach (RunnerToken token in tokens.Values) token.Pause();

            tokens.Clear();
        }

        public void JumpNode(string key, string nodeName)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.JumpTarget = nodeName;
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

        public DialogueWrapper Save()
        {
            if (so == null) return null;

            DialogueWrapper wrapper = new();

            wrapper.SetDatas(tokens, runnerTrigger.Values);

            return wrapper;
        }

        // Restores trigger values immediately, then returns the saved flows as (key, nodeId) pairs for
        // the caller to resume via StartDialogue. Eager (not a lazy iterator) so the trigger restore
        // always runs — even if the caller ignores the result or there are no flows to resume.
        // Call StopAllDialogue() first if the runner already holds flows, so the keys don't collide.
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

        // One-shot restore: clears any flows the runner already holds, restores trigger values, and
        // resumes every saved flow at its stored node. Use this instead of Load when you just want to
        // apply a save without hand-wiring StopAllDialogue + StartDialogue yourself.
        public void LoadAndStart(DialogueWrapper wrapper, IReadOnlyList<IDialogueView> views = null)
        {
            StopAllDialogue();

            foreach (KeyValuePair<string, string> flow in Load(wrapper))
                StartDialogue(flow.Key, flow.Value, views);
        }

        #region Event
        public void ClearEventCallbacks() => runnerEvent.ClearCallbacks();

        public void EventRegisterCallback(string id, System.Action callback) => runnerEvent.RegisterCallback(id, callback);

        public void EventUnregisterCallback(string id, System.Action callback) => runnerEvent.UnregisterCallback(id, callback);
        #endregion

        #region Trigger
        public void ClearTriggerValues() => runnerTrigger.ClearValues();

        public void ClearTriggerCallbacks() => runnerTrigger.ClearCallbacks();

        public bool ContainsKey(string key) => runnerTrigger.ContainsKey(key);

        public bool RemoveValue(string key) => runnerTrigger.RemoveValue(key);

        public void SetValue(string key, float value) => runnerTrigger.SetValue(key, value);

        public void SetValue(string key, bool value) => runnerTrigger.SetValue(key, value);

        public float GetFloatValue(string key) => runnerTrigger.GetFloatValue(key);

        public bool GetBoolValue(string key) => runnerTrigger.GetBoolValue(key);

        public void TriggerRegisterCallback(System.Action<string> callback) => runnerTrigger.OnAnyValueChanged += callback;

        public void TriggerUnregisterCallback(System.Action<string> callback) => runnerTrigger.OnAnyValueChanged -= callback;

        public void TriggerRegisterCallback(string key, System.Action callback) => runnerTrigger.RegisterCallback(key, callback);

        public void TriggerUnregisterCallback(string key, System.Action callback) => runnerTrigger.UnregisterCallback(key, callback);
        #endregion
    }
}