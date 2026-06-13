using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    internal class RunnerToken : IRunnerNodeChecker, IRunnerToken
    {
        private CancellationTokenSource cts;

        public Task Task { get; private set; }

        public bool IsStarted { get; private set; }

        public string PointNode { get; private set; }

        public string TargetNode { get; set; }

        public string JumpTarget { get; set; }

        public event Action OnStopCallback;

        public RunnerToken(string targetNode)
        {
            IsStarted = false;

            PointNode = targetNode;
            JumpTarget = "";

            Resume(targetNode);
        }

        public void Resume(string targetNode)
        {
            if (IsStarted) return;

            IsStarted = true;

            cts = new();

            Task = Task.Delay(Timeout.Infinite, cts.Token);

            TargetNode = targetNode;
        }

        public void Pause()
        {
            if (!IsStarted) return;

            IsStarted = false;

            cts.Cancel();
            cts.Dispose();
            cts = null;

            OnStopCallback?.Invoke();
        }

        public async Task Delay(float second)
        {
            if (!IsStarted) return;

            TimeSpan delay = TimeSpan.FromSeconds(second);

            await Task.Delay(delay, cts.Token);
        }

        public void PointSave() => PointNode = TargetNode;
    }

    public interface IRunnerToken
    {
        public Task Task { get; }

        public bool IsStarted { get; }

        public event Action OnStopCallback;

        public void Pause();

        public Task Delay(float second);
    }

    internal interface IRunnerNodeChecker
    {
        public string TargetNode { get; }

        public string JumpTarget { get; set; }

        public void PointSave();
    }
}