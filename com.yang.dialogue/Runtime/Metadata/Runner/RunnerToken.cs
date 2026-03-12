using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yang.Dialogue
{
    public class RunnerToken : CancellationTokenSource, IRunnerToken
    {
        public bool IsStop => IsCancellationRequested;

        public async Task Delay(float second)
        {
            TimeSpan delay = TimeSpan.FromSeconds(second);

            await Task.Delay(delay, Token);
        }
    }

    public interface IRunnerToken
    {
        public bool IsStop { get; }

        public Task Delay(float second);
    }
}