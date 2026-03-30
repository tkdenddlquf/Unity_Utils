using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    internal class RunnerToken : IRunnerNodeChecker, IRunnerToken
    {
        public RunnerToken(string targetNode) => TargetNode = targetNode;

        private CancellationTokenSource cts = new();

        public bool IsStop => cts == null || cts.IsCancellationRequested;

        public string PointNode { get; private set; }

        public string TargetNode { get; set; }

        public string JumpTarget { get; set; }

        public void Restart(string targetNode)
        {
            if (!IsStop) return;

            cts?.Dispose();
            cts = new();

            TargetNode = targetNode;
        }

        public void Stop()
        {
            if (IsStop) return;

            cts.Cancel();
        }

        public void Dispose()
        {
            if (IsStop) return;

            cts.Dispose();
            cts = null;
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
        public bool IsStop { get; }

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