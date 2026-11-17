using Common.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Framework.Tests.Common;

// The container that built the source disposes it with the host, and with it the NpgsqlDataSource and its pool.
public class DbSourceTests
{
    [Fact]
    public async Task DisposeOfTheContainer_DisposesTheDataSource()
    {
        var configuration = new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                [$"ConnectionStrings:{ConnectionNames.Postgres}"] = "Host=127.0.0.1;Port=1;Timeout=1"
                            })
                            .Build();

        var services = new ServiceCollection()
                       .AddSingleton<IConfiguration>(configuration)
                       .AddSingleton<IDbSource, DbSource>()
                       .BuildServiceProvider();

        var source = services.GetRequiredService<IDbSource>();
        await services.DisposeAsync();

        await FluentActions.Awaiting(() => source.Value.OpenConnectionAsync(TestContext.Current.CancellationToken).AsTask())
                           .Should().ThrowAsync<ObjectDisposedException>();
    }
}
