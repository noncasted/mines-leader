using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class StatePersistenceTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IOrleans orleans) : base(utils, benchmarkStorage)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;
        public override string Title => "state-persistence";
        public override string MetricName => "ms";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var id = Guid.NewGuid();
            Cleanup.Track<TransactionTestState>(id);
            var grain = _orleans.GetGrain<ITransactionTestGrain>(id);

            // Write value via non-transactional path
            var transactions = _orleans.Transactions;
            var result = await transactions.Run(() => grain.Increment());

            if (!result.IsSuccess)
                throw new Exception("Initial transaction failed");

            handle.Progress.Log("Value written, deactivating grain...");
            handle.Progress.SetProgress(0.3f);

            // Force grain deactivation — evicts from memory, clears in-memory cache
            await grain.Deactivate();

            // Wait for Orleans to complete deactivation
            await Task.Delay(2000);

            handle.Progress.Log("Grain deactivated, reading from fresh activation...");
            handle.Progress.SetProgress(0.6f);

            // Read from a fresh grain activation — must load from Postgres
            var value = await grain.Get();

            if (value != 1)
                throw new Exception($"Persistence failed: expected 1, got {value}");

            handle.Progress.Log("Persistence verified: value survived deactivation");
            handle.Progress.SetProgress(1f);
        }
    }
}
