using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    internal class RunnerToken : IRunnerNodeChecker, IRunnerToken
    {
        private CancellationTokenSource cts;

        public Task Task { get; private set; }

        public bool IsStop { get; private set; }

        public string PointNode { get; private set; }

        public string TargetNode { get; set; }

        public string JumpTarget { get; set; }

        public event Action OnStopCallback;

        public RunnerToken(string targetNode)
        {
            IsStop = true;

            PointNode = targetNode;
            JumpTarget = "";

            Restart(targetNode);
        }

        public void Restart(string targetNode)
        {
            if (!IsStop) return;

            IsStop = false;

            cts = new();

            Task = Task.Delay(Timeout.Infinite, cts.Token);

            TargetNode = targetNode;
        }

        public void Stop()
        {
            if (IsStop) return;

            IsStop = true;

            cts.Cancel();
            cts.Dispose();
            cts = null;

            OnStopCallback?.Invoke();
        }

        public async Task Delay(float second)
        {
            if (IsStop) return;

            TimeSpan delay = TimeSpan.FromSeconds(second);

            await Task.Delay(delay, cts.Token);
        }

        public void PointSave() => PointNode = TargetNode;
    }

    public interface IRunnerToken
    {
        public Task Task { get; }

        public bool IsStop { get; }

        public event Action OnStopCallback;

        public void Stop();

        public Task Delay(float second);
    }

    internal interface IRunnerNodeChecker
    {
        public string TargetNode { get; }

        public string JumpTarget { get; set; }

        public void PointSave();
    }
}