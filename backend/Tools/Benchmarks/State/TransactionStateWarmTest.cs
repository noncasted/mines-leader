using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

// Same operation as `transactions-state`, but on a fixed set of already activated grains:
// measures the transaction path without Orleans activation cost per iteration.
public class TransactionStateWarmTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 3300;

        [Id(1)]
        public int Concurrent { get; set; } = 3;

        [Id(2)]
        public int GrainCount { get; set; } = 100;
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
        public override string Title => "transactions-state-warm";
        public override string MetricName => "ops/s";

        protected override async Task Run(BenchmarkNodeHandle handle, StartPayload payload)
        {
            var ids = new Guid[payload.GrainCount];

            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = Guid.NewGuid();
                Cleanup.Track<TransactionTestState>(ids[i]);
                await _orleans.GetGrain<ITransactionTestGrain>(ids[i]).Get();
            }

            var next = -1;

            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);

            return;

            async Task Process()
            {
                var id = ids[(int)((uint)Interlocked.Increment(ref next) % (uint)ids.Length)];
                var grain = _orleans.GetGrain<ITransactionTestGrain>(id);
                var result = await _transactions.Run(() => grain.Increment());

                if (!result.IsSuccess)
                    throw new Exception("Transaction failed");

                handle.Metrics.Inc();
            }
        }
    }
}
