using System.Runtime.ExceptionServices;
using Orleans.Serialization.Invocation;

namespace Infrastructure;

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

    public override T GetResult<T>() => Response.GetResult<T>()!;

    public static TransactionResponse Create(Response response, TransactionContext context)
    {
        return new TransactionResponse
        {
            Response = response,
            Context = context
        };
    }
}
