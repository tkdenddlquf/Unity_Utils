using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    public class RunnerToken : CancellationTokenSource, IRunnerToken
    {
        internal string targetNode;

        public bool IsStop => IsCancellationRequested;

        public string PointNode { get; internal set; }

        public bool IsChangedTarget { get; private set; }

        public void Stop() => Cancel();

        public void SetTarget(string nodeName)
        {
            IsChangedTarget = true;
            targetNode = nodeName;
        }

        public async Task Delay(float second)
        {
            TimeSpan delay = TimeSpan.FromSeconds(second);

            await Task.Delay(delay, Token);
        }
    }

    public interface IRunnerToken
    {
        public bool IsStop { get; }

        public string PointNode { get; }

        public void Stop();

        public void SetTarget(string nodeName);

        public Task Delay(float second);
    }
}