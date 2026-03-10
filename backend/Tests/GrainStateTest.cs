using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Tests;

public class GrainStateTest
{
    [GenerateSerializer]
    public class StartPayload
    {
    }

    [GenerateSerializer]
    public class TestState
    {
        [Id(0)]
        public int Inc { get; set; }

        [Id(1)]
        public IGrainABCD GrainAbcd { get; set; }

        [Id(2)]
        public SomeStateA A0 { get; set; }
        
        [Id(3)]
        public SomeStateA A11 { get; set; }
    }

    [GenerateSerializer]
    public class SomeStateA
    {
        [Id(0)]
        public int A1 { get; set; }

        [Id(1)]
        public IGrainABCD A2 { get; set; }

        [Id(2)]
        public SomeStateB A3 { get; set; }
    }

    [GenerateSerializer]
    public class SomeStateB
    {
        [Id(0)]
        public int B1 { get; set; }

        [Id(1)]
        public IGrainABCD B2 { get; set; }
    }

    public interface IGrainABCD : IGrainWithStringKey
    {
        Task Test();
    }

    public class TestGrainAbcd : Grain, IGrainABCD
    {
        public TestGrainAbcd([State] State<TestState> testState)
        {
            _testState = testState;
        }

        private readonly State<TestState> _testState;

        public async Task Test()
        {
            await _testState.Read();
            _testState.Value.Inc += 1;
            var grain2 = GrainFactory.GetGrain<IGrainABCD>("reference-test");
            var grain3 = GrainFactory.GetGrain<IGrainABCD>("asdfasdf");
            var grain4 = GrainFactory.GetGrain<IGrainABCD>(";lj;lj");
            _testState.Value.GrainAbcd = grain2;
            
            _testState.Value.A0 = new SomeStateA
            {
                A1 = _testState.Value.Inc + 12,
                A2 = grain3,
                A3 = new SomeStateB
                {
                    B1 = _testState.Value.Inc + 343,
                    B2 = grain4
                }
            };
            
            _testState.Value.A11 = new SomeStateA
            {
                A1 = _testState.Value.Inc + 122,
                A2 = grain3,
                A3 = new SomeStateB
                {
                    B1 = _testState.Value.Inc + 3343,
                    B2 = grain4
                }
            };
            var self = this;
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

        protected override string Name => "grain-state";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            var grain = _orleans.GetGrain<IGrainABCD>("test");
            await grain.Test();
        }
    }
}