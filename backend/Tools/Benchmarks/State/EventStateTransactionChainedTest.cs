using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class EventStateTransactionChainedTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)] public int ChainLength { get; set; } = 3;
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
        public override string Title => "event-state-transaction-chained";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, () => Process(payload.ChainLength));

            return;

            async Task Process(int chainLength)
            {
                var ids = TestParticipants.Create(_orleans, chainLength);
                var result = await _transactions.Run(() => ids.Run<IEventStateTransactionTestGrain>(grain => grain.Append(1)));

                if (!result.IsSuccess)
                    throw new Exception("Chained transaction failed");

                foreach (var id in ids.Entries)
                {
                    var grain = _orleans.GetGrain<IEventStateTransactionTestGrain>(id);
                    await grain.Deactivate();
                    Cleanup.Track<EventBenchAggregate>(id);
                }

                handle.Metrics.Inc();
            }
        }
    }
}
