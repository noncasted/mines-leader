using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class SideEffectThroughputTest {
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() {
        [Id(0)]
        public int EffectCount { get; set; } = 1000;
    }

    [GenerateSerializer]
    public class ThroughputSideEffect : ISideEffect {
        [Id(0)] public Guid BatchId { get; set; }

        private static readonly ConcurrentDictionary<Guid, int> Counters = new();

        public static int GetCount(Guid batchId) => Counters.GetValueOrDefault(batchId);
        public static void Reset(Guid batchId) => Counters.TryRemove(batchId, out _);

        public Task Execute(IOrleans orleans) {
            Counters.AddOrUpdate(BatchId, 1, (_, c) => c + 1);
            return Task.CompletedTask;
        }
    }

    public class Root : BenchmarkRoot<StartPayload> {
        public Root(ClusterTestUtils utils, ISideEffectsStorage storage) : base(utils) {
            _storage = storage;
        }

        private readonly ISideEffectsStorage _storage;

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "side-effect-throughput";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var batchId = Guid.NewGuid();
            var total = payload.EffectCount;

            for (var i = 0; i < total; i++)
                await _storage.Write(new ThroughputSideEffect { BatchId = batchId });

            handle.Progress.Log($"Enqueued {total} effects, waiting for worker...");

            var lastCount = 0;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            while (lastCount < total) {
                await Task.Delay(10, cts.Token);
                var current = ThroughputSideEffect.GetCount(batchId);
                var delta = current - lastCount;

                for (var i = 0; i < delta; i++)
                    handle.Metrics.Inc();

                lastCount = current;
                handle.Progress.SetProgress((float)lastCount / total);
            }

            ThroughputSideEffect.Reset(batchId);
            handle.Progress.Log($"All {total} effects processed");
        }
    }
}
