using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class EventStateTransactionConcurrentTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)] public int ConcurrentTransactions { get; set; } = 10;
        [Id(1)] public int Iterations { get; set; } = 1000;
        [Id(2)] public int Concurrent { get; set; } = 3;
    }

    public class Root : BenchmarkRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions) : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.State;
        public override string Title => "event-state-transaction-concurrent";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, () => Process(payload.ConcurrentTransactions));

            return;

            async Task Process(int concurrentTransactions)
            {
                var id = Guid.NewGuid();
                var tasks = new List<Task<TransactionResult>>();

                for (var i = 0; i < concurrentTransactions; i++)
                {
                    var grain = _orleans.GetGrain<IEventStateTransactionTestGrain>(id);
                    tasks.Add(_transactions.Run(() => grain.Append(1)));
                }

                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r.IsSuccess);

                if (successCount == 0)
                    throw new Exception("All concurrent transactions failed");

                var g = _orleans.GetGrain<IEventStateTransactionTestGrain>(id);
                await g.Deactivate();
                handle.Metrics.Inc();
                Cleanup.Track<EventBenchAggregate>(id);
            }
        }
    }
}
