using System.Net.Http.Headers;
using System.Text.Json;
using Game.MatchMetrics;
using Meta.Matches;
using Meta.Users;
using MetaGateway.Matches;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Orchestration;
using Tests.Security;

namespace Tests.Meta.Matches;

// The read API of the match history on a real host with the console token, over the fakes of a test.
public sealed class MatchHistoryHost : IAsyncDisposable
{
    private MatchHistoryHost(TestWebHost web)
    {
        _web = web;
    }

    public const string Token = "secret-token";

    private readonly TestWebHost _web;

    public static async Task<MatchHistoryHost> Start(
        MatchSummaryCollectionFake summaries,
        UserCollectionFake users,
        MatchMetricsReaderFake reader)
    {
        var builder = TestWebHost.CreateBuilder("Production", (ConsoleAccess.TokenKey, Token));
        builder.AddConsoleAccess();
        builder.Services.AddSingleton<IMatchSummaryCollection>(summaries);
        builder.Services.AddSingleton<IUserCollection>(users);
        builder.Services.AddSingleton<IMatchMetricsReader>(reader);

        var app = builder.Build();
        app.MapMatchHistoryRead().RequireConsoleToken();

        return new MatchHistoryHost(await TestWebHost.Start(app));
    }

    public Task<HttpResponseMessage> Get(string path, string? token = Token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return _web.Http.SendAsync(request);
    }

    public async Task<JsonElement> GetJson(string path)
    {
        var response = await Get(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    public ValueTask DisposeAsync()
    {
        return _web.DisposeAsync();
    }
}
