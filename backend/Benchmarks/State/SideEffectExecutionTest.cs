using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class SideEffectExecutionTest
{
    [GenerateSerializer]
    public class TestSideEffect : ISideEffect
    {
        [Id(0)]
        public Guid GrainId { get; set; }

        public async Task Execute(IOrleans orleans)
        {
            var grain = orleans.GetGrain<ITransactionTestGrain>(GrainId);
            var result = await orleans.Transactions.Run(() => grain.Increment());

            if (!result.IsSuccess)
                throw new Exception("Side effect transaction failed");
        }
    }

    public interface ISideEffectTestGrain : IGrainWithGuidKey
    {
        [Transaction]
        Task RegisterSideEffect(Guid targetGrainId);
    }

    public class SideEffectTestGrain : Grain, ISideEffectTestGrain
    {
        public Task RegisterSideEffect(Guid targetGrainId)
        {
            var sideEffect = new TestSideEffect { GrainId = targetGrainId };
            sideEffect.AddToTransaction();
            return Task.CompletedTask;
        }
    }

    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IOrleans orleans, ITransactions transactions) : base(utils, benchmarkStorage)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.State;
        public override string Title => "side-effect-execution";
        public override string MetricName => "ms";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var targetId = Guid.NewGuid();
            var sourceId = Guid.NewGuid();
            Cleanup.Track<TransactionTestState>(targetId);

            var sourceGrain = _orleans.GetGrain<ISideEffectTestGrain>(sourceId);

            // Register side effect within a transaction
            var result = await _transactions.Run(() => sourceGrain.RegisterSideEffect(targetId));

            if (!result.IsSuccess)
                throw new Exception("Transaction with side effect failed");

            handle.Progress.Log("Side effect registered, waiting for execution...");
            handle.Progress.SetProgress(0.3f);

            // Wait for SideEffectsWorker to pick up and execute
            var targetGrain = _orleans.GetGrain<ITransactionTestGrain>(targetId);
            var maxWaitMs = 30_000;
            var elapsedMs = 0;
            var value = 0;

            while (elapsedMs < maxWaitMs)
            {
                await Task.Delay(1000);
                elapsedMs += 1000;

                value = await targetGrain.Get();

                if (value == 1)
                    break;

                handle.Progress.SetProgress(0.3f + 0.6f * elapsedMs / maxWaitMs);
                handle.Progress.Log($"Waiting for side effect... ({elapsedMs / 1000}s)");
            }

            if (value != 1)
                throw new Exception(
                    $"Side effect did not execute within {maxWaitMs / 1000}s. Target value: {value}");

            handle.Progress.Log("Side effect executed successfully");
            handle.Progress.SetProgress(1f);
        }
    }
}
