using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class SideEffectTransactionalTest {
    [GenerateSerializer]
    public class TestTransactionalSideEffect : ITransactionalSideEffect {
        [Id(0)] public Guid GrainId { get; set; }

        public async Task Execute(IOrleans orleans) {
            var grain = orleans.GetGrain<ITransactionTestGrain>(GrainId);
            var result = await orleans.Transactions.Run(() => grain.Increment());

            if (!result.IsSuccess)
                throw new Exception("Transactional side effect failed");
        }
    }

    public interface ISideEffectTransactionalTestGrain : IGrainWithGuidKey {
        [Transaction]
        Task RegisterTransactionalSideEffect(Guid targetGrainId);
    }

    public class SideEffectTransactionalTestGrain : Grain, ISideEffectTransactionalTestGrain {
        public Task RegisterTransactionalSideEffect(Guid targetGrainId) {
            var sideEffect = new TestTransactionalSideEffect { GrainId = targetGrainId };
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
        public override string Title => "side-effect-transactional";
        public override string MetricName => "ms";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var targetId = Guid.NewGuid();
            var sourceId = Guid.NewGuid();
            Cleanup.Track<TransactionTestState>(targetId);

            var sourceGrain = _orleans.GetGrain<ISideEffectTransactionalTestGrain>(sourceId);

            // Register transactional side effect
            var result = await _transactions.Run(() => sourceGrain.RegisterTransactionalSideEffect(targetId));

            if (!result.IsSuccess)
                throw new Exception("Transaction with transactional side effect failed");

            handle.Progress.Log("Transactional side effect registered, waiting for execution...");
            handle.Progress.SetProgress(0.3f);

            // Wait for SideEffectsWorker to pick up and execute (transactional path)
            var targetGrain = _orleans.GetGrain<ITransactionTestGrain>(targetId);
            var maxWaitMs = 30_000;
            var elapsedMs = 0;
            var value = 0;

            while (elapsedMs < maxWaitMs) {
                await Task.Delay(500);
                elapsedMs += 500;

                value = await targetGrain.Get();

                if (value == 1)
                    break;

                handle.Progress.SetProgress(0.3f + 0.6f * elapsedMs / maxWaitMs);
                handle.Progress.Log($"Waiting for transactional side effect... ({elapsedMs / 1000}s)");
            }

            if (value != 1)
                throw new Exception($"Transactional side effect did not execute within {maxWaitMs / 1000}s. Target value: {value}");

            handle.Progress.Log("Transactional side effect executed successfully");
            handle.Progress.SetProgress(1f);
        }
    }
}
