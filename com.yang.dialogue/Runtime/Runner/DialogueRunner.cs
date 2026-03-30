using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private DialogueSO so;

        [SerializeField] private List<DialogueViewBase> viewBases = new();

        private readonly List<IDialogueView> views = new();
        public List<IDialogueView> Views => views;

        private RunnerNode runnerNode;
        private readonly RunnerEvent runnerEvent = new();
        private readonly RunnerTrigger runnerTrigger = new();

        private readonly DialogueWrapper wrapper = new();

        private readonly Dictionary<string, RunnerToken> tokens = new();

        private void Awake() => Init();

        private void Init()
        {
            runnerNode = new(runnerEvent, runnerTrigger);

            views.InsertRange(0, viewBases);

            SetDialogue(so);
        }

        public void SetDialogue(DialogueSO so)
        {
            if (so == null) return;

            bool isStarted = false;

            foreach (RunnerToken token in tokens.Values)
            {
                if (!token.IsStop)
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

        public async void StartDialogue(string key, string nodeName = "", List<IDialogueView> views = null)
        {
            if (so == null) return;

            string nextNode;

            if (tokens.TryGetValue(key, out RunnerToken token))
            {
                if (token.IsStop)
                {
                    if (runnerNode.CheckNode(nodeName)) nextNode = nodeName;
                    else nextNode = token.TargetNode == "" ? so.StartGuid : token.TargetNode;

                    token.Restart(nextNode);
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
                int portIndex = await runnerNode.NextNode(views, token, token);

                if (portIndex == -1)
                {
                    foreach (IDialogueView view in views) await view.OnMessage("", token);

                    break;
                }
                else
                {
                    if (!token.IsChangedTarget || !runnerNode.CheckNode(token.TargetNode))
                    {
                        RunnerPort port = new(token.TargetNode, portIndex);

                        if (runnerNode.TryGetLink(port, out string result)) token.TargetNode = result;
                        else token.TargetNode = token.PointNode;
                    }
                }
            }

            if (!token.IsStop)
            {
                tokens.Remove(key);

                token.Dispose();
            }
        }

        public bool IsStarted(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) return !token.IsStop;

            return false;
        }

        public void StopDialogue(string key)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.Stop();
        }

        public void JumpNode(string key, string nodeName)
        {
            if (tokens.TryGetValue(key, out RunnerToken token)) token.SetTarget(nodeName);
        }

        public DialogueWrapper Save()
        {
            if (so == null) return null;

            wrapper.SetDatas(tokens, runnerTrigger.Triggers);

            return wrapper;
        }

        public void Load(DialogueWrapper wrapper)
        {
            if (so == null && wrapper == null) return;

            for (int i = 0; i < wrapper.Keys.Count; i++)
            {
                RunnerToken token = new(wrapper.Names[i]);

                tokens.Add(wrapper.Keys[i], token);
            }

            runnerTrigger.SetDatas(wrapper.Triggers);
        }

        #region Event
        public void EventRegisterCallback(string id, System.Action<bool> callback) => runnerEvent.RegisterCallback(id, callback);

        public void EventUnregisterCallback(string id, System.Action<bool> callback) => runnerEvent.UnregisterCallback(id, callback);
        #endregion

        #region Trigger
        public bool IsTrigger(string trigger) => runnerTrigger.IsTrigger(trigger);

        public void SetTrigger(string trigger) => runnerTrigger.SetTrigger(trigger);

        public bool UnsetTrigger(string trigger) => runnerTrigger.UnsetTrigger(trigger);

        public void TriggerRegisterCallback(string id, System.Action<bool> callback) => runnerTrigger.RegisterCallback(id, callback);

        public void TriggerUnregisterCallback(string id, System.Action<bool> callback) => runnerTrigger.UnregisterCallback(id, callback);
        #endregion
    }
}