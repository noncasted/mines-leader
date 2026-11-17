namespace Infrastructure.State;

public interface IEventStateValue : IStateValue
{
    // The stream id. Marten sets it on the inline snapshot and in AggregateStreamAsync through the identity member,
    // which may be non-public: { get; init; } or [JsonProperty] { get; private set; }.
    string Id { get; }
}
