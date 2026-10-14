using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    public abstract class DialogueViewBase : MonoBehaviour, IDialogueView
    {
        public abstract string ViewID { get; }

        public virtual async Awaitable OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token) => await Awaitable.NextFrameAsync();

        public virtual async Awaitable<int> OnChoice(RunnerText speaker, RunnerChoiceCollection texts, string message, IRunnerToken token)
        {
            await Awaitable.NextFrameAsync();
            return -1;
        }

        public virtual async Awaitable OnCommand(IReadOnlyList<RunnerCommand> commands, IRunnerToken token) => await Awaitable.NextFrameAsync();

        public virtual async Awaitable OnMessage(string reason, IRunnerToken token) => await Awaitable.NextFrameAsync();

        public virtual void OnPaused(string key) { }

        public virtual void OnStopped(string key) { }

        public virtual void OnEnded(string key) { }

        public virtual object CaptureView(string key) => null;
    }

    public interface IDialogueView
    {
        public string ViewID { get; }

        public Awaitable OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token);

        public Awaitable<int> OnChoice(RunnerText speaker, RunnerChoiceCollection texts, string message, IRunnerToken token);

        public Awaitable OnCommand(IReadOnlyList<RunnerCommand> commands, IRunnerToken token);

        public Awaitable OnMessage(string reason, IRunnerToken token);

        public void OnPaused(string key);

        public void OnStopped(string key);

        public void OnEnded(string key);

        public object CaptureView(string key);
    }
}
