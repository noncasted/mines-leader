using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Xunit;

namespace Framework.Tests.State;

// The request classes of [Transaction] and the call filter: what reaches the caller and what happens to the context.
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionRequestTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    public static TheoryData<string> Kinds => new()
    {
        nameof(ITxRequestKindsGrain.ValueTaskVoid),
        nameof(ITxRequestKindsGrain.ValueTaskResult),
        nameof(ITxRequestKindsGrain.TaskVoid),
        nameof(ITxRequestKindsGrain.TaskResult)
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Request_EndsWell_CommitsAndGivesTheResult(string kind)
    {
        var grain = GetGrain<ITxRequestKindsGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();

        foreach (var mode in new[] { TxRequestMode.Completed, TxRequestMode.Delayed })
        {
            var value = await transactions.Run(() => Call(grain, kind, mode));
            value.Should().Be(TxRequestKindsGrain.Result, $"{kind} {mode}");
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Request_Fails_TheErrorFailsTheTransaction(string kind)
    {
        var grain = GetGrain<ITxRequestKindsGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();

        foreach (var mode in new[] { TxRequestMode.Faulted, TxRequestMode.ThrowsAtCall, TxRequestMode.ThrowsAfterDelay })
        {
            var result = await transactions.Run(async () => {
                await Call(grain, kind, mode);
            });

            result.IsSuccess.Should().BeFalse($"{kind} {mode}");
            result.Error.Should().BeOfType<InvalidOperationException>($"{kind} {mode}")
                  .Which.Message.Should().Be(TxRequestKindsGrain.Failure);
        }
    }

    [Fact]
    public async Task Request_OutsideTransaction_Throws()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());

        var call = () => grain.Increment();

        await call.Should().ThrowAsync<Exception>().WithMessage("TransactionContext is required*");
    }

    // The failed call brought no snapshot, so Transactions.Process asks the participant for it.
    [Fact]
    public async Task Request_FailureCaughtByTheAction_CommitsTheParticipantWithoutSnapshot()
    {
        var grain = GetGrain<ITxRequestKindsGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(async () => {
            var call = () => grain.IncrementThenThrow();
            await call.Should().ThrowAsync<InvalidOperationException>().WithMessage(TxRequestKindsGrain.Failure);
        });

        result.IsSuccess.Should().BeTrue();

        await grain.DeactivateAndWaitNewActivation();
        (await grain.Get()).Should().Be(1);
    }

    [Fact]
    public async Task Request_GrainHeldByAnotherTransaction_JoinFailureFailsOnlyTheSecond()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();

        var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var holding = transactions.Run(async () => {
            await grain.Increment();
            joined.SetResult();
            await resume.Task;
        });

        await joined.Task;

        TransactionResult second;

        using (OverrideConfig<ITransactionConfig, TransactionOptions>(new TransactionOptions
               {
                   LockWaitSeconds = 0.01f,
                   StuckGraceSeconds = 600f
               }))
        {
            second = await transactions.Run(() => grain.Increment());
        }

        resume.SetResult();

        second.IsSuccess.Should().BeFalse();
        second.Error!.Message.Should().StartWith("Handler failed to join");
        (await holding).IsSuccess.Should().BeTrue();
        (await grain.Get()).Should().Be(1);
    }

    private static async Task<int> Call(ITxRequestKindsGrain grain, string kind, TxRequestMode mode)
    {
        switch (kind)
        {
            case nameof(ITxRequestKindsGrain.ValueTaskVoid):
                await grain.ValueTaskVoid(mode);
                return TxRequestKindsGrain.Result;
            case nameof(ITxRequestKindsGrain.ValueTaskResult):
                return await grain.ValueTaskResult(mode);
            case nameof(ITxRequestKindsGrain.TaskVoid):
                await grain.TaskVoid(mode);
                return TxRequestKindsGrain.Result;
            case nameof(ITxRequestKindsGrain.TaskResult):
                return await grain.TaskResult(mode);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
}
