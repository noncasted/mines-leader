using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Tests;

public class TransactionStateChainedTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int ChainLength { get; set; } = 3;

        [Id(1)]
        public int Iterations { get; set; } = 100;

        [Id(2)]
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
        public override string Title => "transactions-state-chained";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, () => Process(payload.ChainLength));
        }

        private async Task Process(int chainLength)
        {
            var ids = TestParticipants.Create(_orleans, chainLength);
            var result = await _transactions.Run(() => ids.Run<ITransactionTestGrain>(grain => grain.Increment()));

            if (result.IsSuccess == false)
                throw new Exception("Chained transaction failed");
        }
    }
}