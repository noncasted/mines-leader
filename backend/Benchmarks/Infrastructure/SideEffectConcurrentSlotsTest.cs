using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class SideEffectConcurrentSlotsTest {
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() {
        [Id(0)] public int EffectCount { get; set; } = 20;
    }

    [GenerateSerializer]
    public class SlotTestSideEffect : ISideEffect {
        [Id(0)] public Guid GrainId { get; set; }

        public async Task Execute(IOrleans orleans) {
            var grain = orleans.GetGrain<ITransactionTestGrain>(GrainId);
            var result = await orleans.Transactions.Run(() => grain.Increment());

            if (!result.IsSuccess)
                throw new Exception("Concurrent slot side effect failed");
        }
    }

    public interface ISideEffectSlotTestGrain : IGrainWithGuidKey {
        [Transaction]
        Task RegisterSlotSideEffect(Guid targetGrainId);
    }

    public class SideEffectSlotTestGrain : Grain, ISideEffectSlotTestGrain {
        public Task RegisterSlotSideEffect(Guid targetGrainId) {
            var sideEffect = new SlotTestSideEffect { GrainId = targetGrainId };
            sideEffect.AddToTransaction();
            return Task.CompletedTask;
        }
    }

    public class Root : ClusterTestRoot<StartPayload> {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IOrleans orleans, ITransactions transactions) : base(utils, benchmarkStorage) {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "side-effect-concurrent-slots";
        public override string MetricName => "ms";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var targetIds = new List<Guid>();

            // Register N side effects in separate transactions
            for (var i = 0; i < payload.EffectCount; i++) {
                var targetId = Guid.NewGuid();
                var sourceId = Guid.NewGuid();
                targetIds.Add(targetId);
                Cleanup.Track<TransactionTestState>(targetId);

                var sourceGrain = _orleans.GetGrain<ISideEffectSlotTestGrain>(sourceId);
                var result = await _transactions.Run(() => sourceGrain.RegisterSlotSideEffect(targetId));

                if (!result.IsSuccess)
                    throw new Exception($"Failed to register side effect {i}");

                handle.Progress.SetProgress(0.3f * (i + 1) / payload.EffectCount);
            }

            handle.Progress.Log($"Registered {payload.EffectCount} side effects, waiting for all to complete...");

            // Wait for all side effects to execute
            var maxWaitMs = 60_000;
            var elapsedMs = 0;
            var completedCount = 0;

            while (elapsedMs < maxWaitMs) {
                await Task.Delay(500);
                elapsedMs += 500;

                completedCount = 0;

                foreach (var targetId in targetIds) {
                    var grain = _orleans.GetGrain<ITransactionTestGrain>(targetId);
                    var value = await grain.Get();

                    if (value == 1)
                        completedCount++;
                }

                handle.Progress.SetProgress(0.3f + 0.7f * completedCount / payload.EffectCount);
                handle.Progress.Log($"Completed: {completedCount}/{payload.EffectCount} ({elapsedMs / 1000}s)");

                if (completedCount == payload.EffectCount)
                    break;
            }

            if (completedCount != payload.EffectCount)
                throw new Exception($"Only {completedCount}/{payload.EffectCount} side effects completed within {maxWaitMs / 1000}s");

            handle.Progress.Log($"All {payload.EffectCount} side effects completed in {elapsedMs}ms");
            handle.Progress.SetProgress(1f);
        }
    }
}
