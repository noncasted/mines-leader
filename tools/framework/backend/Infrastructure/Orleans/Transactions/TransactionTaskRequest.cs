using Orleans.Serialization.Invocation;

namespace Infrastructure;

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
            var resultTask = InvokeInner();

            if (resultTask.IsCompleted)
            {
                resultTask.GetAwaiter().GetResult();
                return new ValueTask<Response>(Response.Completed);
            }

            return CompleteInvoke(resultTask);
        }
        catch (Exception exception)
        {
            return new ValueTask<Response>(Response.FromException(exception));
        }
    }

    private static async ValueTask<Response> CompleteInvoke(Task resultTask)
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
