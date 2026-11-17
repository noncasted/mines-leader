using Common.Extensions;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Xunit;

namespace Framework.Tests.State;

public interface ITxConfirmFailingGrain : IGrainWithGuidKey
{
    [Transaction]
    Task Write(int value);

    Task<int> GetConfirmCalls();
    Task<int> GetRollbackCalls();
}

// Registers a participant whose OnTransactionSuccess always throws, i.e. a failure after the commit.
public class TxConfirmFailingGrain : Grain, ITxConfirmFailingGrain
{
    private readonly ConfirmFailingParticipant _participant = new();

    public Task Write(int value)
    {
        _participant.SetValue(TxTestState.Of(value));

        var handler = (GrainTransactionHandler)GrainContext.GetComponent<IGrainTransactionHandler>().ThrowIfNull();
        handler.RecordStateChanged(_participant);

        return Task.CompletedTask;
    }

    public Task<int> GetConfirmCalls()
    {
        return Task.FromResult(_participant.ConfirmCalls);
    }

    public Task<int> GetRollbackCalls()
    {
        return Task.FromResult(_participant.RollbackCalls);
    }

    private class ConfirmFailingParticipant : IGrainStateTransactionParticipant
    {
        public TxTestState? Value { get; private set; }
        public int ConfirmCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public void SetValue(TxTestState value)
        {
            Value = value;
        }

        public IStateValue GetState()
        {
            return Value.ThrowIfNull();
        }

        public void OnTransactionSuccess()
        {
            ConfirmCalls++;
            throw new InvalidOperationException("Intentional failure after commit");
        }

        public void OnTransactionFailure()
        {
            RollbackCalls++;
        }
    }
}
