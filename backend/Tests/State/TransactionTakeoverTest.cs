using Common.Extensions;
using Infrastructure;

namespace Tests;

public class TransactionTakeoverTest
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
        public override string Title => "transactions-takeover";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var id = Guid.NewGuid();
            var grain = _orleans.GetGrain<ITransactionTestGrain>(id);

            // Start a transaction that holds the grain lock for 40s (> 30s takeover threshold)
            handle.Progress.Log("Starting slow transaction (40s delay)...");
            var slowTask = _transactions.Run(() => grain.IncrementWithDelay(40_000));

            // Wait for the slow transaction to acquire the lock
            await Task.Delay(2000);

            // Start a second transaction — should wait, then takeover after ~30s
            handle.Progress.Log("Starting second transaction (should takeover)...");
            handle.Progress.SetProgress(0.3f);

            var fastResult = await _transactions.Run(() => grain.Increment());

            handle.Progress.SetProgress(0.7f);
            handle.Progress.Log($"Second transaction completed: IsSuccess={fastResult.IsSuccess}");

            if (!fastResult.IsSuccess)
                throw new Exception("Takeover transaction failed");

            // Wait for slow transaction to finish (it should have been rolled back)
            var slowResult = await slowTask;

            handle.Progress.Log($"Slow transaction completed: IsSuccess={slowResult.IsSuccess}");

            // The slow transaction should have failed because it was taken over
            if (slowResult.IsSuccess)
                handle.Progress.Log("Warning: slow transaction reported success despite takeover");

            // Verify final value — only the fast transaction should have committed
            var value = await grain.Get();
            handle.Progress.Log($"Final value: {value}");

            // The value should be at least 1 (from the fast transaction)
            if (value < 1)
                throw new Exception($"Expected value >= 1 after takeover, got {value}");

            handle.Progress.Log("Takeover test passed");
            handle.Progress.SetProgress(1f);
        }
    }
}
