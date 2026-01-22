using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Dialogue
{
    public abstract class DialogueViewBase : MonoBehaviour, IDialogueView
    {
        public virtual Task OnDialogue(RunnerData data, RunnerToken token) => Task.CompletedTask;

        public virtual Task<int> OnChoice(IReadOnlyList<RunnerData> datas, RunnerToken token) => Task.FromResult(-1);
    }

    public interface IDialogueView
    {
        public Task OnDialogue(RunnerData data, RunnerToken token);
        public Task<int> OnChoice(IReadOnlyList<RunnerData> datas, RunnerToken token);
    }
}