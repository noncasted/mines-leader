using System.Net;

namespace Framework.Tests.Console.BackendTraces;

// The dashboard for the reader: answers every request by the given function and keeps the requests.
public sealed class FakeDashboardHandler : HttpMessageHandler
{
    public FakeDashboardHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
    {
        _answer = answer;
    }

    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _answer;
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public static FakeDashboardHandler Status(HttpStatusCode status, string body = "{}")
    {
        return new FakeDashboardHandler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return _answer(request, cancellationToken);
    }
}
