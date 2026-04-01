using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Common.Reactive;

namespace Benchmarks;

public class ReactiveViewableDictTest {
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() {
        [Id(0)] public int SubscriberCount { get; set; } = 10;
        [Id(1)] public int Iterations { get; set; } = 1000;
    }

    public class Root : ClusterTestRoot<StartPayload> {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage) {
        }

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "reactive-viewable-dict";
        public override string MetricName => "ops/s";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var dict = new ViewableDictionary<Guid, string>();
            var parentLifetime = new Lifetime();
            var addCount = 0;
            var removeCount = 0;

            for (var i = 0; i < payload.SubscriberCount; i++)
                dict.Advise(parentLifetime, (_, _, _) => Interlocked.Increment(ref addCount));

            handle.Progress.Log($"Registered {payload.SubscriberCount} subscribers");

            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < payload.Iterations; i++) {
                var key = Guid.NewGuid();
                dict.Add(key, $"value-{i}");
                dict.Remove(key);
                removeCount++;

                if (i % 100 == 0) {
                    handle.Progress.SetProgress((float)i / payload.Iterations);
                    await Task.Yield();
                }
            }

            stopwatch.Stop();

            // Each iteration = 1 Add + 1 Remove = 2 ops
            var totalOps = payload.Iterations * 2;
            var opsPerSecond = totalOps / stopwatch.Elapsed.TotalSeconds;
            handle.ReportMetric(opsPerSecond);

            parentLifetime.Terminate();
            dict.Dispose();

            handle.Progress.Log($"Add+Remove cycles: {payload.Iterations}, subscriber notifications: {addCount}");
            handle.Progress.SetProgress(1f);
        }
    }
}
