using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class SideEffectRetryCycleTest {
    [GenerateSerializer]
    public class FailOnceSideEffect : ISideEffect {
        [Id(0)] public Guid GrainId { get; set; }
        [Id(1)] public bool ShouldFail { get; set; } = true;

        public async Task Execute(IOrleans orleans) {
            var grain = orleans.GetGrain<ITransactionTestGrain>(GrainId);
            var value = await grain.Get();

            // Fail on first attempt (value == 0), succeed on retry (value > 0 means another effect ran)
            if (value == 0 && ShouldFail) {
                // Mark that we attempted by incrementing
                await orleans.Transactions.Run(() => grain.Increment());
                throw new Exception("Intentional failure for retry test");
            }

            // On retry, increment again to signal success
            await orleans.Transactions.Run(() => grain.Increment());
        }
    }

    public interface ISideEffectRetryTestGrain : IGrainWithGuidKey {
        [Transaction]
        Task RegisterRetryEffect(Guid targetGrainId);
    }

    public class SideEffectRetryTestGrain : Grain, ISideEffectRetryTestGrain {
        public Task RegisterRetryEffect(Guid targetGrainId) {
            var sideEffect = new FailOnceSideEffect { GrainId = targetGrainId };
            sideEffect.AddToTransaction();
            return Task.CompletedTask;
        }
    }

    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload> {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IOrleans orleans, ITransactions transactions) : base(utils, benchmarkStorage) {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "side-effect-retry-cycle";
        public override string MetricName => "ms";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var targetId = Guid.NewGuid();
            var sourceId = Guid.NewGuid();
            Cleanup.Track<TransactionTestState>(targetId);

            var sourceGrain = _orleans.GetGrain<ISideEffectRetryTestGrain>(sourceId);

            var result = await _transactions.Run(() => sourceGrain.RegisterRetryEffect(targetId));

            if (!result.IsSuccess)
                throw new Exception("Transaction with retry side effect failed");

            handle.Progress.Log("Failing side effect registered, waiting for retry cycle...");
            handle.Progress.SetProgress(0.2f);

            // Wait for: first attempt (fail) -> retry delay -> second attempt (succeed)
            var targetGrain = _orleans.GetGrain<ITransactionTestGrain>(targetId);
            var maxWaitMs = 120_000; // Retry delay can be up to 30s+ depending on config
            var elapsedMs = 0;
            var value = 0;

            while (elapsedMs < maxWaitMs) {
                await Task.Delay(1000);
                elapsedMs += 1000;

                value = await targetGrain.Get();

                // value >= 2 means: first attempt incremented (then threw), retry incremented again
                if (value >= 2)
                    break;

                handle.Progress.SetProgress(0.2f + 0.7f * elapsedMs / maxWaitMs);
                handle.Progress.Log($"Waiting for retry... value={value} ({elapsedMs / 1000}s)");
            }

            if (value < 2)
                throw new Exception($"Side effect retry did not complete within {maxWaitMs / 1000}s. Target value: {value}");

            handle.Progress.Log($"Retry cycle completed in {elapsedMs}ms, final value: {value}");
            handle.Progress.SetProgress(1f);
        }
    }
}
