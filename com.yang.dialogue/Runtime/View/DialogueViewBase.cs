using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Dialogue
{
    public abstract class DialogueViewBase : MonoBehaviour, IDialogueView
    {
        public virtual Task OnDialogue(RunnerText speaker, RunnerText text, string message, RunnerToken token) => Task.CompletedTask;

        public virtual Task<int> OnChoice(RunnerText speaker, IReadOnlyList<RunnerText> texts, string message, RunnerToken token) => Task.FromResult(-1);
    }

    public interface IDialogueView
    {
        public Task OnDialogue(RunnerText speaker, RunnerText text, string message, RunnerToken token);
        public Task<int> OnChoice(RunnerText speaker, IReadOnlyList<RunnerText> texts, string message, RunnerToken token);
    }
}