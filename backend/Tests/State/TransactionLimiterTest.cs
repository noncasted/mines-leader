using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Tests;

public class TransactionLimiterTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
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
        public override string Title => "Overwriting transactions";
        protected override string Name => "chained-state";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var processedCount = 0;
            var totalCount = payload.Iterations * payload.Concurrent;

            for (var i = 0; i < payload.Iterations; i++)
            {
                var tasks = new List<Task>();

                for (int j = 0; j < payload.Concurrent; j++)
                    tasks.Add(Process(payload.ChainLength, OnProcessed));

                await Task.WhenAll(tasks);
            }

            return;

            void OnProcessed()
            {
                var count = Interlocked.Increment(ref processedCount);
                handle.Progress.SetProgress((float)count / totalCount);
                handle.Progress.Log($"Processed {count}/{totalCount} transactions");
            }
        }

        private async Task Process(int chainLength, Action onProcessed)
        {
            var ids = new List<Guid>();

            for (var i = 0; i < chainLength; i++)
                ids.Add(Guid.NewGuid());

            var taskA = _transactions.Run(async () =>
                {
                    foreach (var id in ids)
                    {
                        var grain = _orleans.GetGrain<ITransactionTestGrain>(id);
                        await grain.Increment();
                    }
                }
            );

            var taskB = _transactions.Run(async () =>
                {
                    foreach (var id in ids)
                    {
                        var grain = _orleans.GetGrain<ITransactionTestGrain>(id);
                        await grain.Increment();
                    }
                }
            );
            
            var resultA = await taskA;
            var resultB = await taskB;
            
            if (resultA.IsSuccess == false || resultB.IsSuccess == false)
                throw new Exception("Chained transaction failed");

            onProcessed();
        }
    }
}