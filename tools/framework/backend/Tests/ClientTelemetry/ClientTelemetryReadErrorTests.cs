using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Framework.Tests.Security;
using Game.ClientTelemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Npgsql;
using Orleans.Runtime;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// 503 asks the reader to repeat: only a failure of the database, a timeout or a cluster that could not answer; any other failure is
// a bug of the server, 500.
public class ClientTelemetryReadErrorTests : IAsyncLifetime
{
    private readonly IClientTelemetryReader _reader = Substitute.For<IClientTelemetryReader>();
    private TestWebHost _host = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var builder = TestWebHost.CreateBuilder("Production");
        builder.Services.AddSingleton(_reader);

        var app = builder.Build();
        app.UseRouting();
        app.MapClientTelemetryRead();

        _host = await TestWebHost.Start(app);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task AFailureOfTheDatabase_ATimeout_OrAnUnavailableSilo_Is503()
    {
        (await Status(new NpgsqlException("down"))).Should().Be(HttpStatusCode.ServiceUnavailable);
        (await Status(new TimeoutException())).Should().Be(HttpStatusCode.ServiceUnavailable);
        (await Status(new SiloUnavailableException("gone"))).Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task AnyOtherFailure_Is500()
    {
        (await Status(new InvalidOperationException("bug"))).Should().Be(HttpStatusCode.InternalServerError);
        (await Status(new NullReferenceException())).Should().Be(HttpStatusCode.InternalServerError);
    }

    private async Task<HttpStatusCode> Status(Exception exception)
    {
        var id = Guid.NewGuid();
        _reader.GetSession(id).ThrowsAsync(exception);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClientTelemetryRoutes.Prefix}/sessions/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "unused");
        using var response = await _host.Http.SendAsync(request, Cancel);

        return response.StatusCode;
    }
}
