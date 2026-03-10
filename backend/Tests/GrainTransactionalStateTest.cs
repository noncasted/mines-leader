using Common.Extensions;
using Infrastructure;
using Infrastructure.State;

namespace Tests;

public class GrainTransactionalStateTest
{
    [GenerateSerializer]
    public class StartPayload
    {
    }



    public interface ITransactionTestGrain : IGrainWithStringKey
    {
        [Infrastructure.State.Transaction]
        Task Test();
    }

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions) : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        protected override string Name => "grain-state";

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            var grain = _orleans.GetGrain<ITransactionStateTestGrain>("test-transactional");
            var result = await _transactions.Run(() => grain.Test());

            if (result.IsSuccess == false)
                throw new Exception();
        }
    }
}