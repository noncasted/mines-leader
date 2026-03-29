using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Tests;

public class TransactionStateValueTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int TransactionCount { get; set; } = 50;
    }

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions) : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.State;
        public override string Title => "transactions-state-value";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var id = Guid.NewGuid();
            var grain = _orleans.GetGrain<ITransactionTestGrain>(id);

            for (var i = 0; i < payload.TransactionCount; i++)
            {
                var result = await _transactions.Run(() => grain.Increment());

                if (!result.IsSuccess)
                    throw new Exception($"Transaction {i + 1} failed");

                handle.Progress.SetProgress((float)(i + 1) / payload.TransactionCount);
            }

            var value = await grain.Get();

            if (value != payload.TransactionCount)
                throw new Exception(
                    $"Value mismatch: expected {payload.TransactionCount}, got {value}. " +
                    $"Lost {payload.TransactionCount - value} updates");

            handle.Progress.Log($"Verified: {value} == {payload.TransactionCount}");
        }
    }
}
