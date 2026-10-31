using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Yang.Dialogue
{
    internal class RunnerToken : IRunnerToken
    {
        private CancellationTokenSource cts;
        private CancellationToken cancellationToken;

        private readonly List<IDialogueView> views = new();
        private readonly Dictionary<string, RunnerChoiceCollection> choiceCaches = new();

        public TaskCompletionSource<bool> ExecutionCompletion { get; set; }
        public bool IsExecuting { get; set; }
        public bool NotifyStoppedAfterRetire { get; set; }
        public int JumpRevision { get; set; }
        public TokenState State { get; private set; } = TokenState.Paused;
        public string TargetNode { get; set; }
        public int NodeIndex { get; set; } = -1;
        public IReadOnlyList<IDialogueView> Views => views;
        public CancellationToken CancellationToken => cancellationToken;

        public RunnerToken() { }

        public RunnerToken(IReadOnlyList<IDialogueView> views)
        {
            SetView(views);
            SetState(TokenState.Running);
        }

        public void SetView(IReadOnlyList<IDialogueView> views)
        {
            if (ReferenceEquals(views, this.views)) return;
            this.views.Clear();
            for (int i = 0; i < views.Count; i++)
                this.views.Add(views[i]);
        }

        public void SetState(TokenState state)
        {
            if (State == state) return;
            State = state;

            if (state == TokenState.Running)
            {
                cts = new();
                cancellationToken = cts.Token;
                return;
            }

            CancellationTokenSource previous = cts;
            cts = null;
            if (previous == null) return;
            previous.Cancel();
            previous.Dispose();
        }

        public async Awaitable Delay(float second)
        {
            if (State != TokenState.Running) return;

            try
            {
                await Awaitable.WaitForSecondsAsync(second, cts.Token);
            }
            catch (OperationCanceledException) when (State != TokenState.Running)
            {
            }
        }

        public async Awaitable<RunnerWaitResult> WaitFor(AwaitableCompletionSource source)
        {
            CancellationToken cancellationToken = CancellationToken;
            using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((AwaitableCompletionSource)state).TrySetCanceled(), source);

            try
            {
                await source.Awaitable;
                return new RunnerWaitResult(RunnerWaitStatus.Completed);
            }
            catch (OperationCanceledException)
            {
                return new RunnerWaitResult(cancellationToken.IsCancellationRequested ? RunnerWaitStatus.TokenCanceled : RunnerWaitStatus.SourceCanceled);
            }
        }

        public async Awaitable<RunnerWaitResult<T>> WaitFor<T>(AwaitableCompletionSource<T> source)
        {
            CancellationToken cancellationToken = CancellationToken;
            using CancellationTokenRegistration registration = cancellationToken.Register(static state => ((AwaitableCompletionSource<T>)state).TrySetCanceled(), source);

            try
            {
                T value = await source.Awaitable;
                return new RunnerWaitResult<T>(RunnerWaitStatus.Completed, value);
            }
            catch (OperationCanceledException)
            {
                return new RunnerWaitResult<T>(cancellationToken.IsCancellationRequested ? RunnerWaitStatus.TokenCanceled : RunnerWaitStatus.SourceCanceled, default);
            }
        }

        public void RefreshView()
        {
            for (int i = views.Count - 1; i >= 0; i--)
            {
                IDialogueView view = views[i];
                if (view == null || (view is UnityEngine.Object unityView && unityView == null))
                    views.RemoveAt(i);
            }
        }

        internal void ClearChoiceCaches() => choiceCaches.Clear();

        internal RunnerChoiceCollection GetChoiceCache(string nodeGuid, IReadOnlyList<DataWrapper> textEntries)
        {
            if (choiceCaches.TryGetValue(nodeGuid, out RunnerChoiceCollection cache)) return cache;
            cache = new RunnerChoiceCollection(textEntries.Count);

            for (int i = 0; i < textEntries.Count; i++)
            {
                int conditionCount = Math.Max(0, (textEntries[i].data.Count - 3) / 3);
                cache.ConditionBuffers[i] = new RunnerCondition[conditionCount];
            }

            choiceCaches.Add(nodeGuid, cache);
            return cache;
        }
    }

    public interface IRunnerToken
    {
        public CancellationToken CancellationToken { get; }

        public TokenState State { get; }

        public IReadOnlyList<IDialogueView> Views { get; }

        public Awaitable Delay(float second);

        public Awaitable<RunnerWaitResult> WaitFor(AwaitableCompletionSource source);

        public Awaitable<RunnerWaitResult<T>> WaitFor<T>(AwaitableCompletionSource<T> source);
    }

    public enum TokenState
    {
        Running,
        Paused,
        Stopped,
        Ended,
    }

    public enum RunnerWaitStatus
    {
        Completed,
        TokenCanceled,
        SourceCanceled,
    }

    public readonly struct RunnerWaitResult
    {
        public RunnerWaitStatus Status { get; }
        public bool IsCompleted => Status == RunnerWaitStatus.Completed;

        public RunnerWaitResult(RunnerWaitStatus status)
        {
            Status = status;
        }
    }

    public readonly struct RunnerWaitResult<T>
    {
        public RunnerWaitStatus Status { get; }
        public bool IsCompleted => Status == RunnerWaitStatus.Completed;
        public T Value { get; }

        public RunnerWaitResult(RunnerWaitStatus status, T value)
        {
            Status = status;
            Value = value;
        }
    }
}
