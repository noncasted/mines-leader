using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Infrastructure;
using Xunit;

namespace Framework.Tests.State;

// The stuck-transaction takeover of GrainTransactionOwner.Join: a transaction taken over must not commit its old
// snapshot over the one of the transaction that took the grain.
[Collection(nameof(OrleansIntegrationCollection))]
public class TransactionTakeoverTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Takeover_StalledTransactionResumes_NoLostUpdate()
    {
        var grain = GetGrain<ITxTestGrain>(Guid.NewGuid());
        var transactions = GetSiloService<ITransactions>();

        var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The lock wait is longer than the grace: when the second Join times out the first transaction is past its
        // grace however late the second one came, so the takeover does not depend on the speed of the machine.
        using var timings = OverrideConfig<ITransactionConfig, TransactionOptions>(new TransactionOptions
        {
            LockWaitSeconds = 0.4f,
            StuckGraceSeconds = 0.2f
        });

        // The first transaction joins and stalls; the second one times out and takes the grain over; only then the
        // first one resumes.
        var stalled = transactions.Run(async () => {
            await grain.Increment();
            joined.SetResult();
            await resume.Task;
        });

        await joined.Task;

        // The grace also bounds the commit (TransactionWriteBatch): the values of the fixture are back before that.
        var takeover = await transactions.Run(async () => {
            await grain.Increment();
            timings.Dispose();
        });

        resume.SetResult();
        var stalledResult = await stalled;

        takeover.IsSuccess.Should().BeTrue();

        await grain.DeactivateAndWaitNewActivation();

        var succeeded = (stalledResult.IsSuccess ? 1 : 0) + 1;
        var value = await grain.Get();
        value.Should().Be(succeeded, "every transaction reported successful must be in the database");
    }
}
