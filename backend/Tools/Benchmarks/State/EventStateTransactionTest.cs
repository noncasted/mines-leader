using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class EventStateTransactionTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)] public int Iterations { get; set; } = 3300;
        [Id(1)] public int Concurrent { get; set; } = 3;
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
        public override string Title => "event-state-transaction";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var id = Guid.NewGuid();
                var grain = _orleans.GetGrain<IEventStateTransactionTestGrain>(id);
                var result = await _transactions.Run(() => grain.Append(1));

                if (!result.IsSuccess)
                    throw new Exception("Transaction failed");

                await grain.Deactivate();
                handle.Metrics.Inc();
                Cleanup.Track<EventBenchAggregate>(id);
            }
        }
    }
}
