using System;

namespace Shared
{
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
}
