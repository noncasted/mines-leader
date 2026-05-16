using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class EventStateTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)] public int Iterations { get; set; } = 3300;
        [Id(1)] public int Concurrent { get; set; } = 10;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans) : base(utils)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;
        public override string Title => "event-state";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var id = Guid.NewGuid();
                var grain = _orleans.GetGrain<IEventStateTestGrain>(id);
                await grain.Append(1);
                await grain.Deactivate();
                handle.Metrics.Inc();
                Cleanup.Track<EventBenchAggregate>(id);
            }
        }
    }
}
