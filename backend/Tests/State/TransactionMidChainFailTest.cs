using Common.Extensions;
using Infrastructure;

namespace Tests;

public class TransactionMidChainFailTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions) : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.State;
        public override string Title => "transactions-mid-chain-fail";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var grainA = _orleans.GetGrain<ITransactionTestGrain>(Guid.NewGuid());
            var grainB = _orleans.GetGrain<ITransactionTestGrain>(Guid.NewGuid());

            // Transaction: A succeeds, B throws — both should rollback
            var result = await _transactions.Run(async () =>
            {
                await grainA.Increment();
                await grainB.Increment();
                throw new Exception("Failure after both grains incremented");
            });

            if (result.IsSuccess)
                throw new Exception("Transaction should have failed");

            handle.Progress.SetProgress(0.4f);

            // Verify both grains rolled back
            var valueA = await grainA.Get();
            var valueB = await grainB.Get();

            if (valueA != 0)
                throw new Exception($"Grain A should be 0 after rollback, got {valueA}");

            if (valueB != 0)
                throw new Exception($"Grain B should be 0 after rollback, got {valueB}");

            handle.Progress.Log("Both grains rolled back correctly");
            handle.Progress.SetProgress(0.6f);

            // Verify both grains are usable for next transaction
            var retryResult = await _transactions.Run(async () =>
            {
                await grainA.Increment();
                await grainB.Increment();
            });

            if (!retryResult.IsSuccess)
                throw new Exception("Retry transaction on rolled-back grains failed");

            var finalA = await grainA.Get();
            var finalB = await grainB.Get();

            if (finalA != 1 || finalB != 1)
                throw new Exception($"After retry: A={finalA}, B={finalB}, expected both 1");

            handle.Progress.Log("Grains usable after mid-chain rollback");
            handle.Progress.SetProgress(1f);
        }
    }
}
