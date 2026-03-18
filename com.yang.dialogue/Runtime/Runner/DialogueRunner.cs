using System.Collections.Generic;
using Unity.Plastic.Antlr3.Runtime;
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

        private readonly Dictionary<string, RunnerTask> tasks = new();

        public event System.Action EndCallback;
        public event System.Action<string, System.Action> StopCallback
        {
            add
            {
                if (runnerNode != null) runnerNode.StopCallback += value;
            }

            remove
            {
                if (runnerNode != null) runnerNode.StopCallback -= value;
            }
        }

        public bool IsStarted { get; private set; }

        private void Awake() => Init();

        private void Init()
        {
            runnerNode = new(runnerEvent, runnerTrigger);

            views.InsertRange(0, viewBases);

            SetDialogue(so);
        }

        public void SetDialogue(DialogueSO so)
        {
            if (so == null || IsStarted) return;

            this.so = so;

            tasks.Clear();

            runnerNode.SetDatas(so);
        }

        public async void StartDialogue(string key, string nodeName = "")
        {
            if (so == null || IsStarted) return;

            IsStarted = true;

            string nextNode;

            if (runnerNode.CheckNode(nodeName)) nextNode = nodeName;
            else if (tasks.TryGetValue(key, out RunnerTask task)) nextNode = task.currentNode;
            else nextNode = so.StartGuid;

            tasks.Remove(key);

            RunnerTask newTask = new(nextNode);
            RunnerToken token = newTask.token;

            tasks.Add(key, newTask);

            while (await runnerNode.NextNode(Views, token)) tasks[key] = new(token);

            if (token.IsStop) tasks[key] = new() { currentNode = token.PointNode };
            else
            {
                EndCallback?.Invoke();

                tasks.Remove(key);
            }

            token.Dispose();

            IsStarted = false;
        }

        public void StopDialogue(string key)
        {
            if (tasks.TryGetValue(key, out RunnerTask task)) task.token?.Stop();
        }

        public void JumpNode(string key, string nodeName)
        {
            if (tasks.TryGetValue(key, out RunnerTask task)) task.token?.SetTarget(nodeName);
        }

        public DialogueWrapper Save()
        {
            if (so == null) return null;

            wrapper.SetDatas(tasks, runnerTrigger.Triggers);

            return wrapper;
        }

        public void Load(DialogueWrapper wrapper)
        {
            if (so == null && wrapper == null) return;

            for (int i = 0; i < wrapper.Keys.Count; i++)
            {
                RunnerTask task = new() { currentNode = wrapper.Names[i] };

                tasks.Add(wrapper.Keys[i], task);
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