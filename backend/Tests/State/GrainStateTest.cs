using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Tests;

public class GrainStateTest
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

    [GenerateSerializer]
    public class TestState
    {
        [Id(0)]
        public int Inc { get; set; }

        [Id(1)]
        public IGrain Grain { get; set; }

        [Id(2)]
        public TestStateA A0 { get; set; }
    }

    [GenerateSerializer]
    public class TestStateA
    {
        [Id(0)]
        public int A1 { get; set; }

        [Id(1)]
        public IGrain A2 { get; set; }
    }

    public interface IGrain : IGrainWithStringKey
    {
        Task Test();
    }

    public class TestGrain : Grain, IGrain
    {
        public TestGrain([State] State<TestState> testState)
        {
            _testState = testState;
        }

        private readonly State<TestState> _testState;

        public async Task Test()
        {
            await _testState.Read();
            var grain2 = GrainFactory.GetGrain<IGrain>("reference-test");

            _testState.Value.Inc += 1;
            _testState.Value.Grain = grain2;
            _testState.Value.A0 = new TestStateA()
            {
                A1 = _testState.Value.Inc + 122,
                A2 = grain2
            };
            
            await _testState.Write();
        }
    }

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans) : base(utils)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;
        public override string Title => "Custom grain state";
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
                handle.Progress.Log($"Processed {count}/{totalCount} iterations");
            }
        }

        private async Task Process(Action onProcessed)
        {
            var grain = _orleans.GetGrain<IGrain>(Guid.NewGuid().ToString());
            await grain.Test();
            onProcessed();
        }
    }
}