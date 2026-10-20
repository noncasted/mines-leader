using System;
using MemoryPack;

namespace Shared
{
    public enum ErrorCode : byte
    {
        Internal = 1,
        UnknownCommand = 2,
        InvalidRequest = 3,
        Forbidden = 4,
        NotFound = 5
    }

    // The answer to a request that failed on the other side: the handler threw, there is no handler for its type, or
    // the session refused it (SessionRequestException). Message names the request and the failure in short, without
    // a stack trace (the stack is in the log of the side that failed). The side that sent the request turns it into
    // NetworkRequestException. A game action refused by the rules of the match is not an error of the request: it is
    // still answered with EmptyResponse.Fail.
    [MemoryPackable]
    public partial class ErrorResponse : INetworkContext
    {
        public ErrorCode Code { get; set; }
        public string Message { get; set; }

        public static ErrorResponse Create(ErrorCode code, INetworkContext request, Exception exception)
        {
            return new ErrorResponse
            {
                Code = code,
                Message = $"{Describe(request)}: {exception.GetType().Name}: {exception.Message}"
            };
        }

        public static ErrorResponse UnknownCommand(INetworkContext request)
        {
            return new ErrorResponse
            {
                Code = ErrorCode.UnknownCommand,
                Message = $"{Describe(request)}: no command for this request"
            };
        }

        // Contexts are nested types with bare names ("Request"): the owner says which request it was.
        public static string Describe(INetworkContext context)
        {
            var type = context.GetType();
            return type.DeclaringType == null ? type.Name : $"{type.DeclaringType.Name}.{type.Name}";
        }
    }

    // A request answered with ErrorResponse.
    public class NetworkRequestException : Exception
    {
        public NetworkRequestException(ErrorCode code, string message, string requestName)
            : base($"Request {requestName} failed on the other side: {code}: {message}")
        {
            Code = code;
            RequestName = requestName;
        }

        public ErrorCode Code { get; }
        public string RequestName { get; }
    }

    [MemoryPackUnion(75, typeof(ErrorResponse))]
    public partial interface INetworkContext
    {
    }
}
