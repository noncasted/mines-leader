using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Tests;

public class ChainedTransactionStateTestFail
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int ChainLength { get; set; } = 3;

        [Id(1)]
        public int Iterations { get; set; } = 100;

        [Id(3)]
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
        public override string Title => "Chained transactional state rollback on error";
        protected override string Name => "chained-state-fail";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, () => Process(payload.ChainLength));
        }

        private async Task Process(int chainLength)
        {
            var ids = TestParticipants.Create(_orleans, chainLength);
            var initialState = await ids.Get<int, ITransactionTestGrain>(grain => grain.Get());

            var failResult = await _transactions.Run(async () =>
                {
                    await ids.Run<ITransactionTestGrain>(grain => grain.Increment());
                    throw new Exception("Intentional rollback");
                }
            );

            if (failResult.IsSuccess)
                throw new Exception("Transaction should have failed but succeeded");

            for (var index = 0; index < ids.Count; index++)
            {
                var id = ids.Entries[index];
                var grain = _orleans.GetGrain<ITransactionTestGrain>(id);
                var value = await grain.Get();
                var initialValue = initialState[index];

                if (value != initialValue)
                    throw new Exception($"Rollback failed for grain {id}: expected {initialValue}, got {value}");
            }
        }
    }
}