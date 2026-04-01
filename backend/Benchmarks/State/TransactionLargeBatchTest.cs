using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class TransactionLargeBatchTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload()
    {
        [Id(0)]
        public int GrainCount { get; set; } = 50;
    }

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IOrleans orleans, ITransactions transactions) : base(utils, benchmarkStorage)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.State;
        public override string Title => "transactions-large-batch";
        public override string MetricName => "ms";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var ids = TestParticipants.Create(_orleans, payload.GrainCount);

            foreach (var id in ids.Entries)
                Cleanup.Track<TransactionTestState>(id);

            handle.Progress.Log($"Starting transaction with {payload.GrainCount} grains...");

            var result = await _transactions.Run(() =>
                ids.Run<ITransactionTestGrain>(grain => grain.Increment()));

            if (!result.IsSuccess)
                throw new Exception($"Large batch transaction with {payload.GrainCount} grains failed");

            handle.Progress.SetProgress(0.7f);
            handle.Progress.Log("Transaction committed, verifying values...");

            // Verify all grains got incremented
            var values = await ids.Get<int, ITransactionTestGrain>(grain => grain.Get());

            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] != 1)
                    throw new Exception(
                        $"Grain {ids.Entries[i]} has value {values[i]}, expected 1");
            }

            handle.Progress.Log($"All {payload.GrainCount} grains verified");
            handle.Progress.SetProgress(1f);
        }
    }
}
