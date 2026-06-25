using System;
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

        /// <summary>
        /// The task that stays pending while the flow runs and is cancelled when it pauses.
        /// </summary>
        public Task Task { get; private set; }

        /// <summary>
        /// Whether this flow is currently running.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// The node saved as the resume point for this flow.
        /// </summary>
        public string PointNode { get; private set; }

        /// <summary>
        /// The node the flow is currently positioned at.
        /// </summary>
        public string TargetNode { get; set; }

        /// <summary>
        /// A queued node to jump to next, or empty when no jump is pending.
        /// </summary>
        public string JumpTarget { get; set; }

        /// <summary>
        /// Raised when the flow is paused or stopped.
        /// </summary>
        public event Action OnStopCallback;

        /// <summary>
        /// Creates a token positioned at the given node and starts it running.
        /// </summary>
        public RunnerToken(string targetNode)
        {
            IsStarted = false;

            PointNode = targetNode;
            JumpTarget = "";

            Resume(targetNode);
        }

        /// <summary>
        /// Marks the flow as running again, creating a fresh cancellation token and pending task at the given node.
        /// </summary>
        public void Resume(string targetNode)
        {
            if (IsStarted) return;

            IsStarted = true;

            cts = new();

            Task = Task.Delay(Timeout.Infinite, cts.Token);

            TargetNode = targetNode;
        }

        /// <summary>
        /// Stops the flow, cancels and disposes its task, and raises the stop callback.
        /// </summary>
        public void Pause()
        {
            if (!IsStarted) return;

            IsStarted = false;

            cts.Cancel();
            cts.Dispose();
            cts = null;

            OnStopCallback?.Invoke();
        }

        /// <summary>
        /// Awaits the given number of seconds, cancelling early if the flow is paused.
        /// </summary>
        public async Task Delay(float second)
        {
            if (!IsStarted) return;

            TimeSpan delay = TimeSpan.FromSeconds(second);

            await Task.Delay(delay, cts.Token);
        }

        /// <summary>
        /// Records the current target node as the resume point.
        /// </summary>
        public void PointSave() => PointNode = TargetNode;
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
        public bool IsStarted { get; }

        /// <summary>
        /// Raised when the flow is paused or stopped; subscribe to clean up view state.
        /// </summary>
        public event Action OnStopCallback;

        /// <summary>
        /// Pauses the flow from within a view callback.
        /// </summary>
        public void Pause();

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

        /// <summary>
        /// A queued node to jump to next, or empty when none is pending.
        /// </summary>
        public string JumpTarget { get; set; }

        /// <summary>
        /// Records the current target node as the resume point.
        /// </summary>
        public void PointSave();
    }
}