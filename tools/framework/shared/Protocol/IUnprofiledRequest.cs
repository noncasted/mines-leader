namespace Shared
{
    // A request the client does not record as a span of its running trace. For a request that repeats through the whole game:
    // a span per request would hide the steps of the trace and use up its limit of spans.
    public interface IUnprofiledRequest : INetworkContext
    {
    }
}
