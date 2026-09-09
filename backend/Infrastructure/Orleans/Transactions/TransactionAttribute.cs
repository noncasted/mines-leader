using System.Runtime.ExceptionServices;
using Common.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;
using Orleans.Serialization.Invocation;

namespace Infrastructure;

[InvokableBaseType(typeof(GrainReference), typeof(ValueTask), typeof(TransactionRequest))]
[InvokableBaseType(typeof(GrainReference), typeof(ValueTask<>), typeof(TransactionRequest<>))]
[InvokableBaseType(typeof(GrainReference), typeof(Task), typeof(TransactionTaskRequest))]
[InvokableBaseType(typeof(GrainReference), typeof(Task<>), typeof(TransactionTaskRequest<>))]
[AttributeUsage(AttributeTargets.Method)]
public class TransactionAttribute : Attribute
{
}

[GenerateSerializer]
public abstract class TransactionRequestBase : RequestBase, IOutgoingGrainCallFilter, IOnDeserialized
{
    [GeneratedActivatorConstructor]
    protected TransactionRequestBase(IServiceProvider sp)
    {
        _grains = sp.GetRequiredService<IGrainFactory>();
    }

    [NonSerialized]
    private readonly IGrainFactory _grains;

    [Id(0)]
    public TransactionContext? Context { get; set; }

    [Id(1)]
    public IAddressable Target { get; set; } = null!;

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
            handler = ResolveHandler(castedTarget);
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

            if (response.Exception != null)
                Context.ExceptionMessage = response.Exception.Message;
            else
                Context.AddResult(participantId, await handler.CollectResult(Context.Id));

            return TransactionResponse.Create(response, Context);
        }
        catch (Exception e)
        {
            Context.ExceptionMessage = e.Message;
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
    // Falls back to the reference when the target is not a grain (should not happen for [Transaction] methods).
    private IGrainTransactionHandler ResolveHandler(IGrainTransactionHandler reference)
    {
        if (GetTarget() is IGrainBase grain)
            return grain.GrainContext.GetGrainExtension<IGrainTransactionHandler>();

        return reference;
    }

    void IOnDeserialized.OnDeserialized(DeserializationContext context)
    {
    }
}

[GenerateSerializer]
public sealed class TransactionResponse : Response
{
    [Id(0)]
    public required Response Response { get; init; }

    [Id(1)]
    public required TransactionContext Context { get; init; }

    public override object? Result
    {
        get
        {
            if (Response.Exception is { } exception)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            return Response.Result;
        }

        set => Response.Result = value;
    }

    public override Exception? Exception
    {
        get
        {
            // Suppress any exception here, allowing ResponseCompletionSource to complete with a Response instead of an exception.
            // This gives TransactionRequestBase a chance to inspect this instance and retrieve the TransactionInfo property first.
            // After, it will use GetException to get and throw the exeption.
            return null;
        }

        set => Response.Exception = value!;
    }

    public Exception? GetException() => Response.Exception;

    public override void Dispose()
    {
        Response.Dispose();
    }

    public override T GetResult<T>() => Response.GetResult<T>();

    public static TransactionResponse Create(Response response, TransactionContext context)
    {
        return new TransactionResponse
        {
            Response = response,
            Context = context
        };
    }
}

[SerializerTransparent]
public abstract class TransactionRequest : TransactionRequestBase
{
    protected TransactionRequest(IServiceProvider grains) : base(grains)
    {
    }

    protected sealed override ValueTask<Response> BaseInvoke()
    {
        try
        {
            var resultTask = InvokeInner();

            if (resultTask.IsCompleted)
            {
                resultTask.GetAwaiter().GetResult();
                return new ValueTask<Response>(Response.Completed);
            }

            return CompleteInvokeAsync(resultTask);
        }
        catch (Exception exception)
        {
            return new ValueTask<Response>(Response.FromException(exception));
        }
    }

    private static async ValueTask<Response> CompleteInvokeAsync(ValueTask resultTask)
    {
        try
        {
            await resultTask;
            return Response.Completed;
        }
        catch (Exception exception)
        {
            return Response.FromException(exception);
        }
    }

    // Generated
    protected abstract ValueTask InvokeInner();
}

[SerializerTransparent]
public abstract class TransactionRequest<TResult> : TransactionRequestBase
{
    protected TransactionRequest(IServiceProvider grains) : base(grains)
    {
    }

    protected sealed override ValueTask<Response> BaseInvoke()
    {
        try
        {
            var resultTask = InvokeInner();

            if (resultTask.IsCompleted)
            {
                return new ValueTask<Response>(Response.FromResult(resultTask.Result));
            }

            return CompleteInvokeAsync(resultTask);
        }
        catch (Exception exception)
        {
            return new ValueTask<Response>(Response.FromException(exception));
        }
    }

    private static async ValueTask<Response> CompleteInvokeAsync(ValueTask<TResult> resultTask)
    {
        try
        {
            var result = await resultTask;
            return Response.FromResult(result);
        }
        catch (Exception exception)
        {
            return Response.FromException(exception);
        }
    }

    // Generated
    protected abstract ValueTask<TResult> InvokeInner();
}

[SerializerTransparent]
public abstract class TransactionTaskRequest<TResult> : TransactionRequestBase
{
    protected TransactionTaskRequest(IServiceProvider grains) : base(grains)
    {
    }

    protected sealed override ValueTask<Response> BaseInvoke()
    {
        try
        {
            var resultTask = InvokeInner();
            var status = resultTask.Status;

            if (resultTask.IsCompleted)
            {
                return new ValueTask<Response>(Response.FromResult(resultTask.GetAwaiter().GetResult()));
            }

            return CompleteInvokeAsync(resultTask);
        }
        catch (Exception exception)
        {
            return new ValueTask<Response>(Response.FromException(exception));
        }
    }

    private static async ValueTask<Response> CompleteInvokeAsync(Task<TResult> resultTask)
    {
        try
        {
            var result = await resultTask;
            return Response.FromResult(result);
        }
        catch (Exception exception)
        {
            return Response.FromException(exception);
        }
    }

    // Generated
    protected abstract Task<TResult> InvokeInner();
}

[SerializerTransparent]
public abstract class TransactionTaskRequest : TransactionRequestBase
{
    protected TransactionTaskRequest(IServiceProvider grains) : base(grains)
    {
    }

    protected sealed override ValueTask<Response> BaseInvoke()
    {
        try
        {
            var target = this.GetTarget();
            var resultTask = InvokeInner();
            var status = resultTask.Status;

            if (resultTask.IsCompleted)
            {
                resultTask.GetAwaiter().GetResult();
                return new ValueTask<Response>(Response.Completed);
            }

            return CompleteInvokeAsync(resultTask);
        }
        catch (Exception exception)
        {
            return new ValueTask<Response>(Response.FromException(exception));
        }
    }

    private static async ValueTask<Response> CompleteInvokeAsync(Task resultTask)
    {
        try
        {
            await resultTask;
            return Response.Completed;
        }
        catch (Exception exception)
        {
            return Response.FromException(exception);
        }
    }

    // Generated
    protected abstract Task InvokeInner();
}