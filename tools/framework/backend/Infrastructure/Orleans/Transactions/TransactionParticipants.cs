using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

// What Transactions.Process asks of the grains that joined: their snapshots, then the end of the transaction.
internal class TransactionParticipants
{
    public TransactionParticipants(ILogger logger)
    {
        _logger = logger;
    }

    private readonly ILogger _logger;

    public async Task<TransactionCommitResult> Collect(TransactionContext context)
    {
        var states = new List<GrainStateRecord>();
        var events = new List<GrainEventRecord>();

        var collections = await Task.WhenAll(context.Participants.Select(p => CollectOne(context, p.Key, p.Value)));

        foreach (var collection in collections)
        {
            states.AddRange(collection.States);
            events.AddRange(collection.Events);
        }

        return new TransactionCommitResult()
        {
            States = states,
            Events = events
        };
    }

    public async Task Confirm(TransactionContext context)
    {
        var confirmations = context.Participants.Select(async participant => {
            try
            {
                await participant.Value.OnSuccess(context.Id);
            }
            catch (Exception e)
            {
                // Not a transaction failure: the data is committed. A handler that threw has released its grain
                // already (GrainTransactionHandler.OnSuccess); one the call did not reach keeps the lock until
                // the stuck-transaction takeover in GrainTransactionOwner.Join reloads its state.
                _logger.LogError(e,
                    "[Transaction] [Error] Participant {ParticipantId} failed to confirm committed transaction {TransactionId}",
                    participant.Key, context.Id);

                TransactionTelemetry.ConfirmFailure.Add(1);
            }
        });

        await Task.WhenAll(confirmations);
    }

    public async Task Rollback(TransactionContext context)
    {
        foreach (var (participantId, participant) in context.Participants)
        {
            try
            {
                await participant.OnFailure(context.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Transactions] Failed to rollback participant {ParticipantId}", participantId);
                TransactionTelemetry.RollbackFailure.Add(1);
            }
        }
    }

    private static async Task<TransactionCommitResult> CollectOne(
        TransactionContext context,
        Guid handlerId,
        IGrainTransactionHandler handler)
    {
        var grainStates = new List<GrainStateRecord>();
        var grainEvents = new List<GrainEventRecord>();

        // The snapshot normally arrives inside TransactionResponse (TransactionRequestBase.Invoke);
        // the RPC is only for participants whose response did not reach this context.
        var result = context.Results.TryGetValue(handlerId, out var snapshot)
            ? snapshot
            : await handler.CollectResult(context.Id);
        var participantId = handler.GetGrainId();

        foreach (var state in result.States)
        {
            grainStates.Add(new GrainStateRecord
            {
                Id = participantId,
                Value = state
            });
        }

        grainEvents.AddRange(result.Events);

        return new TransactionCommitResult
        {
            States = grainStates,
            Events = grainEvents
        };
    }
}
