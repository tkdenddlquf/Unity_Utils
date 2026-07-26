using System.Collections.Generic;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// Base MonoBehaviour for dialogue views. Subclass it and override the callbacks to render dialogue,
    /// choices, and objects in your own UI. The runner awaits each returned Awaitable, so awaiting your UI
    /// (typewriter, button press, etc.) inside an override pauses the conversation until it completes.
    /// </summary>
    public abstract class DialogueViewBase : MonoBehaviour, IDialogueView
    {
        public abstract string ViewID { get; }

        /// <summary>
        /// Fires when a line of dialogue should be shown. Override and return an Awaitable that completes once the
        /// player has finished reading; the runner awaits it before advancing. Base returns immediately.
        /// </summary>
        public virtual Awaitable OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token)
            => Completed();

        /// <summary>
        /// Fires when the player must pick from choices. Override to present options and return the selected
        /// index (the runner awaits it to branch). Base returns -1, meaning no selection.
        /// </summary>
        public virtual Awaitable<int> OnChoice(RunnerText speaker, RunnerChoiceCollection texts, string message, IRunnerToken token)
            => Completed(-1);

        /// <summary>
        /// Fires when the graph emits asset-independent commands. Override to dispatch command ids to
        /// game-specific handlers; the runner awaits the returned Awaitable. Base returns immediately.
        /// </summary>
        public virtual Awaitable OnCommand(IReadOnlyList<RunnerCommand> commands, IRunnerToken token)
            => Completed();

        /// <summary>
        /// Fires when the signals a reason. Override to clean up UI; the runner awaits it.
        /// Base returns immediately.
        /// </summary>
        public virtual Awaitable OnMessage(string reason, IRunnerToken token)
            => Completed();

        /// <summary>Returns an already-completed Awaitable for synchronous View implementations.</summary>
#pragma warning disable CS1998
        protected static async Awaitable Completed() { }

        /// <summary>Returns an already-completed Awaitable containing a synchronous result.</summary>
        protected static async Awaitable<T> Completed<T>(T result) => result;
#pragma warning restore CS1998

        public virtual void OnPaused() { }

        public virtual void OnStopped() { }

        public virtual void OnEnded() { }

        public virtual object CaptureView() => null;
    }

    /// <summary>
    /// Contract for objects that receive dialogue callbacks from the runner. Implement it (or subclass
    /// DialogueViewBase) and register via DialogueRunner.AddView to render dialogue in custom targets.
    /// </summary>
    public interface IDialogueView
    {
        public string ViewID { get; }

        /// <summary>
        /// Called to display a line of dialogue; return an Awaitable that completes when the line is done.
        /// </summary>
        public Awaitable OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token);

        /// <summary>
        /// Called to present choices; return the chosen index so the runner can branch.
        /// </summary>
        public Awaitable<int> OnChoice(RunnerText speaker, RunnerChoiceCollection texts, string message, IRunnerToken token);

        /// <summary>
        /// Called to interpret asset-independent commands emitted by the current node.
        /// </summary>
        public Awaitable OnCommand(IReadOnlyList<RunnerCommand> commands, IRunnerToken token);

        /// <summary>
        /// Called when the conversation ends or emits a reason; use it to finalize the view.
        /// </summary>
        public Awaitable OnMessage(string reason, IRunnerToken token);

        public void OnPaused();

        public void OnStopped();

        public void OnEnded();

        public object CaptureView();
    }
}
