using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

// Same operation as `state`, but on a fixed set of already activated grains:
// measures the state read/write path without Orleans activation cost per iteration.
public class StateWarmTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 3300;

        [Id(1)]
        public int Concurrent { get; set; } = 10;

        [Id(2)]
        public int GrainCount { get; set; } = 100;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans) : base(utils)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;
        public override string Title => "state-warm";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            var keys = new string[payload.GrainCount];

            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = Guid.NewGuid().ToString();
                Cleanup.Track<StateTest.TestState>(keys[i]);
                await _orleans.GetGrain<StateTest.IGrain>(keys[i]).Test();
            }

            var next = -1;

            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var key = keys[(int)((uint)Interlocked.Increment(ref next) % (uint)keys.Length)];
                await _orleans.GetGrain<StateTest.IGrain>(key).Test();

                handle.Metrics.Inc();
            }
        }
    }
}
