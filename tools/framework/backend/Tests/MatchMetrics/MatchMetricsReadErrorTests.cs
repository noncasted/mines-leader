using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Framework.Tests.Security;
using Game.MatchMetrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Framework.Tests.MatchMetrics;

// 503 asks the reader to repeat: only a failure of the database or a timeout; any other failure is a bug of the server, 500.
public class MatchMetricsReadErrorTests : IAsyncLifetime
{
    private readonly ThrowingReader _reader = new();
    private TestWebHost _host = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var builder = TestWebHost.CreateBuilder("Production");
        builder.Services.AddSingleton<IMatchMetricsReader>(_reader);

        var app = builder.Build();
        app.UseRouting();
        app.MapMatchMetricsRead();

        _host = await TestWebHost.Start(app);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    private async Task<HttpStatusCode> Status(Exception exception)
    {
        _reader.Throw(exception);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{MatchMetricsRoutes.Prefix}/series");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "unused");
        using var response = await _host.Http.SendAsync(request, Cancel);

        return response.StatusCode;
    }

    [Fact]
    public async Task A_failure_of_the_database_or_a_timeout_is_503()
    {
        (await Status(new NpgsqlException("down"))).Should().Be(HttpStatusCode.ServiceUnavailable);
        (await Status(new TimeoutException())).Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Any_other_failure_is_500()
    {
        (await Status(new InvalidOperationException("bug"))).Should().Be(HttpStatusCode.InternalServerError);
        (await Status(new NullReferenceException())).Should().Be(HttpStatusCode.InternalServerError);
    }

    private sealed class ThrowingReader : IMatchMetricsReader
    {
        private Exception _exception = null!;

        public void Throw(Exception exception) => _exception = exception;

        public Task<IReadOnlyList<MatchMetricStreamInfo>> GetStreams(Guid matchId) => throw _exception;
        public Task<IReadOnlyList<MatchMetricSeriesInfo>> GetSeries(string? name) => throw _exception;
        public Task<IReadOnlyList<MatchMetricGroupPoints>> GetPoints(Guid matchId, MatchMetricPointsQuery query) => throw _exception;
        public Task<IReadOnlyList<MatchMetricGroupTotal>> GetTotals(MatchMetricTotalsQuery query) => throw _exception;
        public Task<IReadOnlyList<MatchMetricEventInfo>> GetEvents(Guid matchId, MatchMetricEventsQuery query) => throw _exception;
    }
}
