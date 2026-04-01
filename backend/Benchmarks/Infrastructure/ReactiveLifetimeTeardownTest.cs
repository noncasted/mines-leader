using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Common.Reactive;

namespace Benchmarks;

public class ReactiveLifetimeTeardownTest {
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() {
        [Id(0)] public int ListenerCount { get; set; } = 100;
        [Id(1)] public int Iterations { get; set; } = 1000;
    }

    public class Root : ClusterTestRoot<StartPayload> {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "reactive-lifetime-teardown";
        public override string MetricName => "ops/s";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var counter = 0;
            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < payload.Iterations; i++) {
                var lifetime = new Lifetime();

                for (var j = 0; j < payload.ListenerCount; j++)
                    lifetime.Listen(() => Interlocked.Increment(ref counter));

                lifetime.Terminate();

                if (i % 100 == 0) {
                    handle.Progress.SetProgress((float)i / payload.Iterations);
                    await Task.Yield();
                }
            }

            stopwatch.Stop();
            var opsPerSecond = payload.Iterations / stopwatch.Elapsed.TotalSeconds;
            handle.ReportMetric(opsPerSecond);

            handle.Progress.Log($"Teardowns: {payload.Iterations}, listeners per teardown: {payload.ListenerCount}, total callbacks: {counter}");
            handle.Progress.SetProgress(1f);
        }
    }
}
