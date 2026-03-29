using Common.Extensions;
using Infrastructure;

namespace Tests;

public class TransactionEmptyTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, ITransactions transactions) : base(utils)
        {
            _transactions = transactions;
        }

        private readonly ITransactions _transactions;

        public override string Group => TestGroups.State;
        public override string Title => "transactions-empty";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            // Transaction with no grain calls — should succeed cleanly
            var result = await _transactions.Run(() => Task.CompletedTask);

            if (!result.IsSuccess)
                throw new Exception("Empty transaction failed");

            handle.Progress.Log("Empty transaction completed successfully");
            handle.Progress.SetProgress(1f);
        }
    }
}
