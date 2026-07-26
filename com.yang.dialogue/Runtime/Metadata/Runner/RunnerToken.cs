using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Yang.Dialogue
{
    /// <summary>
    /// Per-conversation state object that tracks the current node, run/pause status, and a cancellation-backed
    /// task the runner awaits. Created and managed internally by DialogueRunner for each active flow.
    /// </summary>
    internal class RunnerToken : IRunnerNodeChecker, IRunnerToken
    {
        private CancellationTokenSource cts;

        public AwaitableCompletionSource<bool> UsedTask { get; set; }

        /// <summary>
        /// Whether this flow is currently running.
        /// </summary>
        public TokenState State { get; private set; } = TokenState.Paused;

        /// <summary>
        /// The node the flow is currently positioned at.
        /// </summary>
        public string TargetNode { get; set; }
        public int NodeIndex { get; set; } = -1;

        private readonly List<IDialogueView> views = new();
        private readonly Dictionary<string, RunnerChoiceCollection> choiceCaches = new();
        public IReadOnlyList<IDialogueView> Views => views;
        public CancellationToken CancellationToken => cts?.Token ?? default;

        public RunnerToken()
        {

        }

        /// <summary>
        /// Creates a token positioned at the given node and starts it running.
        /// </summary>
        public RunnerToken(IReadOnlyList<IDialogueView> views)
        {
            SetView(views);
            SetState(TokenState.Running);
        }

        public void SetView(IReadOnlyList<IDialogueView> views)
        {
            if (ReferenceEquals(views, this.views)) return;

            this.views.Clear();

            for (int i = 0; i < views.Count; i++) this.views.Add(views[i]);
        }

        public void SetState(TokenState state)
        {
            if (State == state) return;

            State = state;

            switch (state)
            {
                case TokenState.Running:
                    cts = new();
                    break;

                case TokenState.Paused:
                case TokenState.Stopped:
                case TokenState.Ended:
                    cts?.Cancel();
                    cts?.Dispose();
                    cts = null;
                    break;
            }
        }

        /// <summary>
        /// Awaits the given number of seconds, cancelling early if the flow is paused.
        /// </summary>
        public async Awaitable Delay(float second)
        {
            if (State != TokenState.Running) return;

            try
            {
                await Awaitable.WaitForSecondsAsync(second, cts.Token);
            }
            catch (OperationCanceledException) when (State != TokenState.Running)
            {
                // Pause, stop, and end are normal control-flow transitions.
            }
        }

        /// <summary>
        /// Awaits an externally completed operation and cancels it when this dialogue flow pauses, stops, or ends.
        /// </summary>
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

        /// <summary>
        /// Awaits an externally completed operation and returns its result, cancelling it with this dialogue flow.
        /// </summary>
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
                if (views[i] == null) views.RemoveAt(i);
            }
        }

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

    /// <summary>
    /// Read-only handle to a running conversation, passed to view callbacks. Use it to await delays in sync
    /// with the flow (token.Delay(1f)), check token.IsStarted, pause it, or subscribe to OnStopCallback.
    /// </summary>
    public interface IRunnerToken
    {
        /// <summary>
        /// Cancellation signal tied to the current run. It is cancelled when the flow pauses, stops, or ends.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Whether the flow is currently running.
        /// </summary>
        public TokenState State { get; }

        public IReadOnlyList<IDialogueView> Views { get; }

        /// <summary>
        /// Awaits a delay that cancels with the flow, e.g. await token.Delay(0.5f) inside a view callback.
        /// </summary>
        public Awaitable Delay(float second);

        /// <summary>
        /// Awaits a completion source that is automatically cancelled with the current dialogue flow.
        /// </summary>
        public Awaitable<RunnerWaitResult> WaitFor(AwaitableCompletionSource source);

        /// <summary>
        /// Awaits a completion source result that is automatically cancelled with the current dialogue flow.
        /// </summary>
        public Awaitable<RunnerWaitResult<T>> WaitFor<T>(AwaitableCompletionSource<T> source);
    }

    /// <summary>
    /// Internal interface exposing node navigation state to the runner's node engine.
    /// </summary>
    internal interface IRunnerNodeChecker
    {
        /// <summary>
        /// The node the flow is currently positioned at.
        /// </summary>
        public string TargetNode { get; }
        public int NodeIndex { get; }
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
