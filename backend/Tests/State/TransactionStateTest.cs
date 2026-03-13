using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Tests;

public class TransactionStateTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 10;

        [Id(1)]
        public int Concurrent { get; set; } = 3;
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
        public override string Title => "Custom transactional grain state";
        protected override string Name => "grain-state";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);
        }

        private async Task Process()
        {
            var grain = _orleans.GetGrain<ITransactionTestGrain>(Guid.NewGuid());
            var result = await _transactions.Run(() => grain.Increment());

            if (!result.IsSuccess)
                throw new Exception("Transaction failed");
        }
    }
}