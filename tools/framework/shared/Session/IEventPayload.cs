namespace Shared
{
    // One-shot message on a network entity or service (SharedSessionObject.Event): the backend forwards the bytes
    // without reading them. The union is declared by the project (see IEntityPayload).
    public interface IEventPayload
    {
    }
}
