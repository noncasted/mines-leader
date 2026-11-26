namespace Console.Infrastructure.BackendTraces;

// The error status of a span (STATUS_CODE_ERROR); the message may be empty.
public sealed record BackendTraceError(string Message);
