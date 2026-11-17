using Orleans.Serialization.Invocation;

namespace Infrastructure;

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

            if (resultTask.IsCompleted)
            {
                return new ValueTask<Response>(Response.FromResult(resultTask.GetAwaiter().GetResult()));
            }

            return CompleteInvoke(resultTask);
        }
        catch (Exception exception)
        {
            return new ValueTask<Response>(Response.FromException(exception));
        }
    }

    private static async ValueTask<Response> CompleteInvoke(Task<TResult> resultTask)
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
