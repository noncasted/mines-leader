using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Tests;

public class TransactionConcurrentValueTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int ConcurrentTransactions { get; set; } = 10;
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
        public override string Title => "transactions-concurrent-value";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var id = Guid.NewGuid();
            Cleanup.Track<TransactionTestState>(id);
            var grain = _orleans.GetGrain<ITransactionTestGrain>(id);
            var successCount = 0;

            var tasks = new List<Task<TransactionResult>>();

            for (var i = 0; i < payload.ConcurrentTransactions; i++)
                tasks.Add(_transactions.Run(() => grain.Increment()));

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                if (result.IsSuccess)
                    Interlocked.Increment(ref successCount);
            }

            handle.Progress.SetProgress(0.8f);
            handle.Progress.Log($"Completed: {successCount}/{payload.ConcurrentTransactions} succeeded");

            if (successCount == 0)
                throw new Exception("All transactions failed");

            var value = await grain.Get();

            if (value != successCount)
                throw new Exception(
                    $"Value mismatch: expected {successCount}, got {value}. " +
                    $"Lost {successCount - value} updates");

            handle.Progress.Log($"Verified: value {value} == {successCount} successful transactions");
            handle.Progress.SetProgress(1f);
        }
    }
}
