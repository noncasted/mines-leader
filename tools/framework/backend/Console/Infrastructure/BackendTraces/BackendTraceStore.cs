namespace Console.Infrastructure.BackendTraces;

// How a source names its store on the page: in a failure ("<Name> answered 500") and for a trace it does not have.
public sealed record BackendTraceStore(string Name, string NotFound);
