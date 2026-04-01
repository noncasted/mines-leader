using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Common.Reactive;

namespace Benchmarks;

public class ReactiveEventFanoutTest {
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload {
        [Id(0)] public int Iterations { get; set; } = 100;
        [Id(1)] public int Concurrent { get; set; } = 10;
        [Id(2)] public int SubscriberCount { get; set; } = 50;
    }

    public class Root : ClusterTestRoot<StartPayload> {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "reactive-event-fanout";
        public override string MetricName => "ops/s";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var eventSource = new EventSource<int>();
            var lifetime = new Lifetime();
            var counter = 0;

            for (var i = 0; i < payload.SubscriberCount; i++)
                eventSource.Advise(lifetime, _ => Interlocked.Increment(ref counter));

            handle.Progress.Log($"Registered {payload.SubscriberCount} subscribers");

            await handle.RunConcurrentIterations(payload, () => {
                eventSource.Invoke(42);
                return Task.CompletedTask;
            });

            lifetime.Terminate();
            eventSource.Dispose();

            handle.Progress.Log($"Total handler invocations: {counter}");
        }
    }
}
