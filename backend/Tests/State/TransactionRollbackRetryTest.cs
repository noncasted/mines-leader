using Common.Extensions;
using Infrastructure;

namespace Tests;

public class TransactionRollbackRetryTest
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
        public override string Title => "transactions-rollback-retry";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var id = Guid.NewGuid();
            Cleanup.Track<TransactionTestState>(id);
            var grain = _orleans.GetGrain<ITransactionTestGrain>(id);

            // First transaction: increment then throw — should rollback
            var failResult = await _transactions.Run(async () =>
            {
                await grain.Increment();
                throw new Exception("Intentional failure");
            });

            if (failResult.IsSuccess)
                throw new Exception("Failed transaction reported success");

            handle.Progress.Log("First transaction rolled back");
            handle.Progress.SetProgress(0.3f);

            // Verify value is 0 after rollback
            var valueAfterRollback = await grain.Get();

            if (valueAfterRollback != 0)
                throw new Exception($"Value after rollback should be 0, got {valueAfterRollback}");

            handle.Progress.SetProgress(0.5f);

            // Second transaction: should succeed immediately without waiting for takeover
            var successResult = await _transactions.Run(() => grain.Increment());

            if (!successResult.IsSuccess)
                throw new Exception("Retry transaction failed");

            handle.Progress.SetProgress(0.8f);

            // Verify value is 1
            var finalValue = await grain.Get();

            if (finalValue != 1)
                throw new Exception($"Final value should be 1, got {finalValue}");

            handle.Progress.Log("Rollback + retry verified: semaphore released correctly");
            handle.Progress.SetProgress(1f);
        }
    }
}
