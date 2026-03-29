using Cluster.Discovery;
using Common.Extensions;
using Common.Reactive;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Tests;

public class StateCollectionSyncTest
{
    // --- State ---

    [GenerateSerializer]
    public class CollectionTestState : IStateValue
    {
        [Id(0)]
        public Guid Id { get; set; }

        [Id(1)]
        public string Label { get; set; } = string.Empty;

        public int Version => 0;
    }

    // --- StateCollection ---

    public interface ICollectionTestCollection : IStateCollection<Guid, CollectionTestState>
    {
    }

    public class CollectionTestCollection(StateCollectionUtils<Guid, CollectionTestState> utils)
        : StateCollection<Guid, CollectionTestState>(utils), ICollectionTestCollection;

    // --- Grain that writes state and pushes to collection ---

    public interface ICollectionTestGrain : IGrainWithGuidKey
    {
        [Transaction]
        Task Write(string label);
    }

    public class CollectionTestGrain : Grain, ICollectionTestGrain
    {
        public CollectionTestGrain(
            [State] State<CollectionTestState> state,
            ICollectionTestCollection collection)
        {
            _state = state;
            _collection = collection;
        }

        private readonly State<CollectionTestState> _state;
        private readonly ICollectionTestCollection _collection;

        public async Task Write(string label)
        {
            var updated = await _state.Update(s =>
            {
                s.Id = this.GetPrimaryKey();
                s.Label = label;
            });

            await _collection.OnUpdatedTransactional(updated.Id, updated);
        }
    }

    // --- Payload ---

    [GenerateSerializer]
    public class StartPayload
    {
        [Id(0)]
        public Guid TestId { get; set; }

        [Id(1)]
        public string ExpectedLabel { get; set; } = string.Empty;
    }

    public static string TestName => "state-collection-sync";

    // --- Root: writes via grain, then asks node to verify ---

    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(
            ClusterTestUtils utils,
            IOrleans orleans,
            ITransactions transactions,
            ICollectionTestCollection collection) : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
            _collection = collection;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;
        private readonly ICollectionTestCollection _collection;

        public override string Group => TestGroups.State;
        public override string Title => "state-collection-sync";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            var testId = Guid.NewGuid();
            Cleanup.Track<CollectionTestState>(testId);
            var label = $"test-{testId:N}";

            // Write via transactional grain call
            var grain = _orleans.GetGrain<ICollectionTestGrain>(testId);
            var result = await _transactions.Run(() => grain.Write(label));

            if (!result.IsSuccess)
                throw new Exception("Transaction failed");

            handle.Progress.Log("Value written via transaction");
            handle.Progress.SetProgress(0.3f);

            // Wait for durable queue propagation
            await Task.Delay(3000);

            handle.Progress.SetProgress(0.5f);

            // Verify on local collection first
            if (!_collection.ContainsKey(testId))
                throw new Exception("Local collection did not receive update");

            if (_collection[testId].Label != label)
                throw new Exception($"Local collection label mismatch: expected '{label}', got '{_collection[testId].Label}'");

            handle.Progress.Log("Local collection verified");
            handle.Progress.SetProgress(0.6f);

            // Start node on a different service to verify remote collection
            var nodePayload = new StartPayload
            {
                TestId = testId,
                ExpectedLabel = label
            };

            // Verify on multiple services
            var services = new[] { ServiceTag.Game, ServiceTag.Meta, ServiceTag.Coordinator };

            foreach (var service in services)
            {
                await handle.StartNode(service, TestName, nodePayload);
                handle.Progress.Log($"Node on {service} verified");
            }

            // Give nodes time to verify
            await Task.Delay(3000);

            handle.Progress.Log("All services verified collection sync");
            handle.Progress.SetProgress(1f);
        }
    }

    // --- Node: verifies collection has the value ---

    public class Node : ClusterTestNode<StartPayload>
    {
        public Node(ClusterTestUtils utils, ICollectionTestCollection collection) : base(utils)
        {
            _collection = collection;
        }

        private readonly ICollectionTestCollection _collection;

        protected override string Name => TestName;

        protected override Task Run(IReadOnlyLifetime lifetime, StartPayload payload)
        {
            if (!_collection.ContainsKey(payload.TestId))
                throw new Exception($"Remote collection missing key {payload.TestId}");

            var actual = _collection[payload.TestId].Label;

            if (actual != payload.ExpectedLabel)
                throw new Exception(
                    $"Remote collection label mismatch: expected '{payload.ExpectedLabel}', got '{actual}'");

            Logger.LogInformation("StateCollection sync verified on {Service}: {Label}",
                Environment.Tag, payload.ExpectedLabel);

            return Task.CompletedTask;
        }
    }
}
