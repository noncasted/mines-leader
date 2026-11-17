using Common.Extensions;
using Console;
using Framework.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Console;

public class AuditLogStorageTests
{
    [Fact]
    public async Task GetPage_OfAnUnreachableDatabase_Throws()
    {
        await using var unreachable = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=test;Password=test;Timeout=1");
        var storage = StorageOf(unreachable);

        await Assert.ThrowsAnyAsync<Exception>(() => storage.GetPage(0, 10));
    }

    [Fact]
    public async Task Write_ToAnUnreachableDatabase_Throws()
    {
        await using var unreachable = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=test;Password=test;Timeout=1");
        var storage = StorageOf(unreachable);

        await Assert.ThrowsAnyAsync<Exception>(() => storage.Write("action", "details"));
    }

    [Fact]
    public async Task GetPage_WithoutTheTable_Throws()
    {
        await using var database = new DatabaseFixture();
        await database.Initialize();
        var storage = StorageOf(database.DataSource);

        await Assert.ThrowsAsync<PostgresException>(() => storage.GetPage(0, 10));
    }

    [Fact]
    public async Task Write_AfterTheTableIsCreated_IsRead()
    {
        await using var database = new DatabaseFixture();
        await database.Initialize();

        await using (var connection = await database.DataSource.OpenConnectionAsync())
        {
            await AuditLogTable.Create(connection);
            await AuditLogTable.Create(connection);
        }

        var storage = StorageOf(database.DataSource);
        await storage.Write("first", "a");
        await storage.Write("second", "b");

        var page = await storage.GetPage(0, 10);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["second", "first"], page.Entries.Select(entry => entry.Action));
    }

    private static AuditLogStorage StorageOf(NpgsqlDataSource dataSource)
    {
        var source = Substitute.For<IDbSource>();
        source.Value.Returns(dataSource);
        return new AuditLogStorage(source);
    }
}
