using System.Runtime.ExceptionServices;
using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization.Invocation;

namespace Infrastructure;

[GenerateSerializer]
public abstract class TransactionRequestBase : RequestBase, IOutgoingGrainCallFilter
{
    [GeneratedActivatorConstructor]
    protected TransactionRequestBase(IServiceProvider sp)
    {
        _grains = sp.GetRequiredService<IGrainFactory>();
    }

    [NonSerialized]
    private readonly IGrainFactory _grains;

    [Id(0)]
    public TransactionContext? Context { get; private set; }

    [Id(1)]
    public IAddressable Target { get; private set; } = null!;

    async Task IOutgoingGrainCallFilter.Invoke(IOutgoingGrainCallContext context)
    {
        var currentContext = Context ?? TransactionContextProvider.Current;

        // The target only needs the transaction id: it registers its own participant, side effects and
        // snapshot into the context it receives and sends that back. Sending the caller's accumulated
        // participants/snapshots would grow every request with the number of grains already visited.
        if (currentContext != null && Context == null)
            Context = new TransactionContext { Id = currentContext.Id };

        try
        {
            Target = _grains.GetGrain(context.TargetId)
                            .AsReference<IGrainTransactionHandler>();

            await context.Invoke();
        }
        finally
        {
            if (context.Response is TransactionResponse response)
            {
                var target = currentContext.ThrowIfNull();

                foreach (var (id, participants) in response.Context.Participants)
                    target.Participants.TryAdd(id, participants);

                foreach (var (id, sideEffect) in response.Context.SideEffects)
                    target.SideEffects.TryAdd(id, sideEffect);

                foreach (var (id, result) in response.Context.Results)
                    target.AddResult(id, result);

                if (response.GetException() is { } exception)
                {
                    ExceptionDispatchInfo.Throw(exception);
                }
            }
        }
    }

    public override async ValueTask<Response> Invoke()
    {
        if (Context == null)
            return Response.FromException(new Exception("TransactionContext is required for TransactionRequestBase."));

        IGrainTransactionHandler handler;
        Guid participantId;

        try
        {
            TransactionContextProvider.SetCurrent(Context);
            var castedTarget = Target.AsReference<IGrainTransactionHandler>();
            handler = ResolveHandler();
            participantId = await handler.Join(Context.Id);
            Context.Participants.TryAdd(participantId, castedTarget);
        }
        catch (Exception e)
        {
            TransactionContextProvider.Clear();
            return Response.FromException(e);
        }

        try
        {
            var response = await BaseInvoke();

            if (response.Exception == null)
                Context.AddResult(participantId, await handler.CollectResult(Context.Id));

            return TransactionResponse.Create(response, Context);
        }
        catch (Exception e)
        {
            return TransactionResponse.Create(Response.FromException(e), Context);
        }
        finally
        {
            TransactionContextProvider.Clear();
        }
    }

    protected abstract ValueTask<Response> BaseInvoke();

    // Invoke already runs inside the target activation, so Join and CollectResult are in-memory calls
    // on the grain's IGrainTransactionHandler extension instead of separate RPCs to the same grain.
    // The handler's [AlwaysInterleave] OnSuccess/OnFailure of a previous transaction can still run
    // while Join waits for the lock, exactly as when Join arrived as a message.
    private IGrainTransactionHandler ResolveHandler()
    {
        return ((IGrainBase)GetTarget()).GrainContext.GetGrainExtension<IGrainTransactionHandler>();
    }
}
