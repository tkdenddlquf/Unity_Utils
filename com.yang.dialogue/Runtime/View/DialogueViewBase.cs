using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// Base MonoBehaviour for dialogue views. Subclass it and override the callbacks to render dialogue,
    /// choices, and objects in your own UI. The runner awaits each returned Task, so awaiting your UI
    /// (typewriter, button press, etc.) inside an override pauses the conversation until it completes.
    /// </summary>
    public abstract class DialogueViewBase : MonoBehaviour, IDialogueView
    {
        /// <summary>
        /// Fires when a line of dialogue should be shown. Override and return a Task that completes once the
        /// player has finished reading; the runner awaits it before advancing. Base returns immediately.
        /// </summary>
        public virtual Task OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token) => Task.CompletedTask;

        /// <summary>
        /// Fires when the player must pick from choices. Override to present options and return the selected
        /// index (the runner awaits it to branch). Base returns -1, meaning no selection.
        /// </summary>
        public virtual Task<int> OnChoice(RunnerText speaker, IReadOnlyList<RunnerChoiceText> texts, string message, IRunnerToken token) => Task.FromResult(-1);

        /// <summary>
        /// Fires when associated objects (sprites, audio, etc.) should be applied. Override to handle them;
        /// the runner awaits the returned Task. Base returns immediately.
        /// </summary>
        public virtual Task OnObject(IReadOnlyList<Object> target, IRunnerToken token) => Task.CompletedTask;

        /// <summary>
        /// Fires when the conversation ends or signals a reason. Override to clean up UI; the runner awaits it.
        /// Base returns immediately.
        /// </summary>
        public virtual Task OnMessage(string reason, IRunnerToken token) => Task.CompletedTask;
    }

    /// <summary>
    /// Contract for objects that receive dialogue callbacks from the runner. Implement it (or subclass
    /// DialogueViewBase) and register via DialogueRunner.AddView to render dialogue in custom targets.
    /// </summary>
    public interface IDialogueView
    {
        /// <summary>
        /// Called to display a line of dialogue; return a Task that completes when the line is done.
        /// </summary>
        public Task OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token);

        /// <summary>
        /// Called to present choices; return the chosen index so the runner can branch.
        /// </summary>
        public Task<int> OnChoice(RunnerText speaker, IReadOnlyList<RunnerChoiceText> texts, string message, IRunnerToken token);

        /// <summary>
        /// Called to apply associated objects such as sprites or audio for the current node.
        /// </summary>
        public Task OnObject(IReadOnlyList<Object> target, IRunnerToken token);

        /// <summary>
        /// Called when the conversation ends or emits a reason; use it to finalize the view.
        /// </summary>
        public Task OnMessage(string reason, IRunnerToken token);
    }
}