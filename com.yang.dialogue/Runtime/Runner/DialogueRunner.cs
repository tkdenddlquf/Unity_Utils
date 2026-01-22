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
        private RunnerEvent runnerEvent;
        private RunnerTrigger runnerTrigger;

        private RunnerToken token;

        private readonly DialogueWrapper wrapper = new();

        public bool IsStarted { get; private set; }

        private void Awake() => Init();

        private void Init()
        {
            runnerEvent = new();
            runnerTrigger = new();
            runnerNode = new(this, runnerEvent);

            views.InsertRange(0, viewBases);

            SetDialogue(so);
        }

        public void SetDialogue(DialogueSO so)
        {
            if (so == null || IsStarted) return;

            this.so = so;

            runnerNode.SetDatas(so);
        }

        public async void StartDialogue()
        {
            if (IsStarted) return;

            IsStarted = true;

            string nextNode = runnerNode.CurrentNode;

            token = new();

            while (!token.IsCancellationRequested)
            {
                nextNode = await runnerNode.NextNode(nextNode, token);

                if (nextNode == "") break;
            }

            token.Dispose();
            token = null;

            IsStarted = false;
        }

        public void StopDialogue() => token?.Cancel();

        public DialogueWrapper Save()
        {
            if (so == null) return null;

            wrapper.SetDatas(so.key, runnerNode.CurrentNode, runnerTrigger.Triggers);

            return wrapper;
        }

        public void Load(DialogueWrapper wrapper)
        {
            if (so == null && wrapper == null && so.key != wrapper.key) return;

            runnerNode.JumpNode(wrapper.currentNode);
            runnerTrigger.SetDatas(wrapper.triggers);
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