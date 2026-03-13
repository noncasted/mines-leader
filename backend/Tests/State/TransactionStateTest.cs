using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Tests;

public class TransactionStateTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
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

            var processedCount = 0;
            var totalCount = payload.Iterations * payload.Concurrent;

            for (var i = 0; i < payload.Iterations; i++)
            {
                var tasks = new List<Task>();

                for (var j = 0; j < payload.Concurrent; j++)
                    tasks.Add(Process(OnProcessed));

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

        private async Task Process(Action onProcessed)
        {
            var grain = _orleans.GetGrain<ITransactionTestGrain>(Guid.NewGuid());
            var result = await _transactions.Run(() => grain.Increment());

            if (!result.IsSuccess)
                throw new Exception("Transaction failed");

            onProcessed();
        }
    }
}