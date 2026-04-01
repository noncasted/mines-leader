using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Benchmarks;

public class StateMigrationTest
{
    // --- State versions ---

    [GenerateSerializer]
    public class MigrationTestState_0 : IStateValue
    {
        [Id(0)]
        public int Value { get; set; }

        public int Version => 0;
    }

    [GenerateSerializer]
    public class MigrationTestState_1 : IStateValue
    {
        [Id(0)]
        public int Value { get; set; }

        [Id(1)]
        public string Label { get; set; } = string.Empty;

        public int Version => 1;
    }

    // --- Migration steps ---

    public class MigrationTestStep_V0 : IStateMigrationStep
    {
        public MigrationTestStep_V0(IStateSerializer serializer)
        {
            _serializer = serializer;
        }

        private readonly IStateSerializer _serializer;

        public int Version => 0;
        public Type Type => typeof(MigrationTestState_1);

        public IStateValue Deserialize(string raw)
        {
            return _serializer.TryDeserialize<MigrationTestState_0>(raw)!;
        }

        public IStateValue Migrate(IStateValue value)
        {
            throw new NotSupportedException("V0 step is never a migration target.");
        }
    }

    public class MigrationTestStep_V1 : IStateMigrationStep
    {
        public MigrationTestStep_V1(IStateSerializer serializer)
        {
            _serializer = serializer;
        }

        private readonly IStateSerializer _serializer;

        public int Version => 1;
        public Type Type => typeof(MigrationTestState_1);

        public IStateValue Deserialize(string raw)
        {
            return _serializer.TryDeserialize<MigrationTestState_1>(raw)!;
        }

        public IStateValue Migrate(IStateValue value)
        {
            var v0 = (MigrationTestState_0)value;

            return new MigrationTestState_1
            {
                Value = v0.Value,
                Label = $"migrated-{v0.Value}"
            };
        }
    }

    // --- Grains ---

    public interface IMigrationGrainV0 : IGrainWithStringKey
    {
        Task Write(int value);
    }

    public class MigrationGrainV0 : Grain, IMigrationGrainV0
    {
        public MigrationGrainV0([State] State<MigrationTestState_0> state)
        {
            _state = state;
        }

        private readonly State<MigrationTestState_0> _state;

        public Task Write(int value)
        {
            return _state.Update(s => s.Value = value);
        }
    }

    public interface IMigrationGrainV1 : IGrainWithStringKey
    {
        Task<(int value, string label)> Read();
    }

    public class MigrationGrainV1 : Grain, IMigrationGrainV1
    {
        public MigrationGrainV1([State] State<MigrationTestState_1> state)
        {
            _state = state;
        }

        private readonly State<MigrationTestState_1> _state;

        public async Task<(int value, string label)> Read()
        {
            await _state.Read();
            return (_state.Value.Value, _state.Value.Label);
        }
    }

    // --- Test root ---

    public class Root : ClusterTestRoot<EmptyPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IOrleans orleans) : base(utils, benchmarkStorage)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;
        public override string Title => "state-migration";
        public override string MetricName => "ms";

        protected override async Task Run(ClusterTestNodeHandle handle, EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var key = Guid.NewGuid().ToString();
            const int writtenValue = 42;
            const string expectedLabel = "migrated-42";

            var v0Grain = _orleans.GetGrain<IMigrationGrainV0>(key);
            await v0Grain.Write(writtenValue);

            var v1Grain = _orleans.GetGrain<IMigrationGrainV1>(key);
            var (value, label) = await v1Grain.Read();

            if (value != writtenValue)
                throw new Exception($"Migration failed: expected Value={writtenValue}, got {value}");

            if (label != expectedLabel)
                throw new Exception($"Migration failed: expected Label='{expectedLabel}', got '{label}'");

            handle.Progress.SetProgress(1f);
        }
    }

    [GenerateSerializer]
    public class EmptyPayload
    {
    }
}
