using System.Diagnostics.CodeAnalysis;
using Common.Extensions;

namespace Benchmarks;

public class DbConnectionPoolTest {
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload {
        [Id(0)] public int Iterations { get; set; } = 100;
        [Id(1)] public int Concurrent { get; set; } = 10;
    }

    public class Root : ClusterTestRoot<StartPayload> {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IDbSource dbSource) : base(utils, benchmarkStorage) {
            _dbSource = dbSource;
        }

        private readonly IDbSource _dbSource;

        public override string Group => TestGroups.Infrastructure;
        public override string Title => "db-connection-pool";
        public override string MetricName => "ops/s";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload) {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            await handle.RunConcurrentIterations(payload, async () => {
                await using var connection = await _dbSource.OpenConnection();
            });
        }
    }
}
