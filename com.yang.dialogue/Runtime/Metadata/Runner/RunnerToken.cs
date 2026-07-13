using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    /// <summary>
    /// Per-conversation state object that tracks the current node, run/pause status, and a cancellation-backed
    /// task the runner awaits. Created and managed internally by DialogueRunner for each active flow.
    /// </summary>
    internal class RunnerToken : IRunnerNodeChecker, IRunnerToken
    {
        private CancellationTokenSource cts;

        public TaskCompletionSource<bool> UsedTask { get; set; }

        /// <summary>
        /// The task that stays pending while the flow runs and is cancelled when it pauses.
        /// </summary>
        public Task Task { get; private set; }

        /// <summary>
        /// Whether this flow is currently running.
        /// </summary>
        public TokenState State { get; private set; }

        /// <summary>
        /// The node the flow is currently positioned at.
        /// </summary>
        public string TargetNode { get; set; }

        private readonly List<IDialogueView> views = new();
        public IReadOnlyList<IDialogueView> Views => views;

        public RunnerToken()
        {
            State = TokenState.Paused;
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

                    Task = Task.Delay(Timeout.Infinite, cts.Token);
                    break;

                case TokenState.Paused:
                case TokenState.Stopped:
                case TokenState.Ended:
                    cts.Cancel();
                    cts.Dispose();
                    cts = null;
                    break;
            }
        }

        /// <summary>
        /// Awaits the given number of seconds, cancelling early if the flow is paused.
        /// </summary>
        public async Task Delay(float second)
        {
            if (State != TokenState.Running) return;

            TimeSpan delay = TimeSpan.FromSeconds(second);

            await Task.Delay(delay, cts.Token);
        }

        public void RefreshView()
        {
            for (int i = views.Count - 1; i >= 0; i--)
            {
                if (views[i] == null) views.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Read-only handle to a running conversation, passed to view callbacks. Use it to await delays in sync
    /// with the flow (token.Delay(1f)), check token.IsStarted, pause it, or subscribe to OnStopCallback.
    /// </summary>
    public interface IRunnerToken
    {
        /// <summary>
        /// The task that stays pending while the flow runs.
        /// </summary>
        public Task Task { get; }

        /// <summary>
        /// Whether the flow is currently running.
        /// </summary>
        public TokenState State { get; }

        public IReadOnlyList<IDialogueView> Views { get; }

        /// <summary>
        /// Awaits a delay that cancels with the flow, e.g. await token.Delay(0.5f) inside a view callback.
        /// </summary>
        public Task Delay(float second);
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
    }

    public enum TokenState
    {
        Running,
        Paused,
        Stopped,
        Ended,
    }
}