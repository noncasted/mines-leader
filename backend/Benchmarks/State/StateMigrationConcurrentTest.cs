using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;

namespace Benchmarks;

public class StateMigrationConcurrentTest
{
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload
    {
        [Id(0)]
        public int Iterations { get; set; } = 20;

        [Id(1)]
        public int Concurrent { get; set; } = 5;
    }

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IOrleans orleans) : base(utils, benchmarkStorage)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;
        public override string Title => "state-migration-concurrent";
        public override string MetricName => "ops/s";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            await handle.RunConcurrentIterations(payload, Process);
        }

        private async Task Process()
        {
            var key = Guid.NewGuid().ToString();
            const int writtenValue = 77;

            // Write as V0
            var v0Grain = _orleans.GetGrain<StateMigrationTest.IMigrationGrainV0>(key);
            await v0Grain.Write(writtenValue);

            // Read as V1 — triggers migration
            var v1Grain = _orleans.GetGrain<StateMigrationTest.IMigrationGrainV1>(key);
            var (value, label) = await v1Grain.Read();

            if (value != writtenValue)
                throw new Exception($"Migration value mismatch: expected {writtenValue}, got {value}");

            if (label != $"migrated-{writtenValue}")
                throw new Exception($"Migration label mismatch: expected 'migrated-{writtenValue}', got '{label}'");
        }
    }
}
