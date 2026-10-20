namespace Internal
{
    // The server closed a socket with ProtocolVersion.ClientOutdatedCloseCode: this build speaks another protocol and
    // can only be replaced. Show puts up a blocking notice with the way to update; repeated calls change nothing.
    // IsShown stays true for the rest of the run: nothing reconnects after it, every connect would be refused the same.
    public interface IClientOutdatedNotice
    {
        bool IsShown { get; }

        void Show();
    }

    // A connect stopped because the client is outdated (IClientOutdatedNotice.IsShown): retrying it cannot succeed.
    public class ClientOutdatedException : System.Exception
    {
        public ClientOutdatedException(string message) : base(message)
        {
        }
    }
}
