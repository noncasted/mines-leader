namespace Global.Backend
{
    public class BackendClient : IBackendClient
    {
        public BackendClient(
            IBackendGetGateway get,
            IBackendPostGateway post,
            IBackendMediaGateway media,
            BackendOptions options)
        {
            Get = get;
            Post = post;
            Media = media;
            Options = options;
        }

        public IBackendGetGateway Get { get; }
        public IBackendPostGateway Post { get; }
        public IBackendMediaGateway Media { get; }
        public BackendOptions Options { get; }
    }
}