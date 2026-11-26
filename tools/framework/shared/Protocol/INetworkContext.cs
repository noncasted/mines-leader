namespace Shared
{
    // Every message of the protocol (a request, a response, a push). The union over INetworkContext is declared by
    // the project, not here: its types live in two assemblies (the framework ones here, the game ones in the project
    // shared) and a [MemoryPackable] partial interface cannot span assemblies. The project declares
    //
    //     [MemoryPackUnionFormatter(typeof(INetworkContext))]
    //     [MemoryPackUnion(NetworkContextTags.EmptyResponse, typeof(EmptyResponse))]
    //     [MemoryPackUnion(NetworkContextTags.ErrorResponse, typeof(ErrorResponse))]
    //     public partial class NetworkContextUnion { }
    //
    // and adds the tags of its types next to them, in partial declarations of the same class. The generated
    // NetworkContextUnionInitializer.RegisterFormatter() must run before the first message: on .NET it is a module
    // initializer, in Unity the project calls it on load.
    //
    // A member of type INetworkContext needs [MemoryPackAllowSerialize]: the formatter is registered at runtime.
    public interface INetworkContext
    {
    }
}
