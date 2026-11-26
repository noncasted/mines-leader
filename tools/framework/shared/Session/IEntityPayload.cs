namespace Shared
{
    // The payload a network entity is created with (SharedSessionEntity.CreateRequest / CreatedOverview): what the
    // entity is. The union is declared by the project, as the one over INetworkContext:
    //
    //     [MemoryPackUnionFormatter(typeof(IEntityPayload))]
    //     [MemoryPackUnion(0, typeof(MenuPlayerPayload))]
    //     public partial class EntityPayloadUnion { }
    //
    // and registered with it on start (EntityPayloadUnionInitializer.RegisterFormatter()).
    public interface IEntityPayload
    {
    }
}
