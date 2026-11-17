namespace Cluster.Discovery;

public static class ServerUrlExtensions
{
    public static string ServerUrlToWebSocket(this string url)
    {
        return url.Contains("http://") ? url.Replace("http://", "ws://") : url.Replace("https://", "wss://");
    }
}
