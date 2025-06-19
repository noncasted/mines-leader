namespace Global.Backend
{
    public interface IBackendClient
    {
        IBackendGet Get { get; }
        IBackendPost Post { get; }
        IBackendMedia Media { get; }
        BackendOptions Options { get; }
    }

    public class BackendClient : IBackendClient
    {
        public BackendClient(
            IBackendGet get,
            IBackendPost post,
            IBackendMedia media,
            BackendOptions options)
        {
            Get = get;
            Post = post;
            Media = media;
            Options = options;
        }

        public IBackendGet Get { get; }
        public IBackendPost Post { get; }
        public IBackendMedia Media { get; }
        public BackendOptions Options { get; }
    }
}