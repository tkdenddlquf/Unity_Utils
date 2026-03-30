using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    internal class RunnerToken : IRunnerNodeChecker, IRunnerToken
    {
        public RunnerToken(string targetNode)
        {
            TargetNode = targetNode;
            PointNode = targetNode;
        }

        private CancellationTokenSource cts = new();

        public bool IsStop => cts.IsCancellationRequested;

        public string TargetNode { get; set; }

        public string PointNode { get; private set; }

        public bool IsChangedTarget { get; private set; }

        public void Restart(string targetNode)
        {
            cts?.Dispose();

            cts = new();

            TargetNode = targetNode;
            PointNode = targetNode;
        }

        public void Stop() => cts.Cancel();

        public void Dispose() => cts.Dispose();

        public void SetTarget(string nodeName)
        {
            IsChangedTarget = true;
            TargetNode = nodeName;
        }

        public async Task Delay(float second)
        {
            TimeSpan delay = TimeSpan.FromSeconds(second);

            await Task.Delay(delay, cts.Token);
        }

        public void Apply() => PointNode = TargetNode;
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

        public void SetTarget(string nodeName);

        public void Apply();
    }
}