namespace Global.Backend
{
    public interface IBackendClient
    {
        IBackendGetGateway Get { get; }
        IBackendPostGateway Post { get; }
        IBackendMediaGateway Media { get; }
        BackendOptions Options { get; }
    }
}