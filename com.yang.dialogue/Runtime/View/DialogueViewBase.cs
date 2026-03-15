using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Dialogue
{
    public abstract class DialogueViewBase : MonoBehaviour, IDialogueView
    {
        public virtual Task OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token) => Task.CompletedTask;

        public virtual Task<int> OnChoice(RunnerText speaker, IReadOnlyList<RunnerText> texts, string message, IRunnerToken token) => Task.FromResult(-1);

        public virtual Task OnObject(IReadOnlyList<Object> target, IRunnerToken token) => Task.CompletedTask;
    }

    public interface IDialogueView
    {
        public Task OnDialogue(RunnerText speaker, RunnerText text, string message, IRunnerToken token);

        public Task<int> OnChoice(RunnerText speaker, IReadOnlyList<RunnerText> texts, string message, IRunnerToken token);

        public Task OnObject(IReadOnlyList<Object> target, IRunnerToken token);
    }
}