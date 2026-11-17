using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit;

namespace Framework.Tests.State;

// The monitor operations of the side effects storage over a database that does not answer: the console must see the
// failure, not an empty result or a "done".
public class SideEffectsStorageErrorTests : IAsyncDisposable
{
    public SideEffectsStorageErrorTests()
    {
        // Nothing listens on port 1: every connection is refused.
        _dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=test;Password=test;Timeout=2");

        var dbSource = Substitute.For<IDbSource>();
        dbSource.Value.Returns(_dataSource);

        _storage = new SideEffectsStorage(dbSource, new JsonTestSerializer(), NullLogger<SideEffectsStorage>.Instance);
    }

    private readonly NpgsqlDataSource _dataSource;
    private readonly SideEffectsStorage _storage;

    public ValueTask DisposeAsync()
    {
        return _dataSource.DisposeAsync();
    }

    [Fact]
    public async Task GetStats_DatabaseDown_Throws()
    {
        var act = () => _storage.GetStats();
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetRetryEntries_DatabaseDown_Throws()
    {
        var act = () => _storage.GetRetryEntries(10);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DropRetryEntry_DatabaseDown_Throws()
    {
        var act = () => _storage.DropRetryEntry(Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task RequeueRetryEntry_DatabaseDown_Throws()
    {
        var act = () => _storage.RequeueRetryEntry(Guid.NewGuid());
        await act.Should().ThrowAsync<Exception>();
    }
}
