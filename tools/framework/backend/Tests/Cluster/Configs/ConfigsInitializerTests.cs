using Cluster.Configs;
using FluentAssertions;
using Infrastructure;
using Infrastructure.Execution;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Framework.Tests.Cluster.Configs;

// The seed of a config is read from the content root of the host, not from the folder of the process binaries: the
// hosts of a monolith share one AppContext.BaseDirectory. A missing file is a setup error.
public class ConfigsInitializerTests : IDisposable
{
    public ConfigsInitializerTests()
    {
        _environment.ContentRootPath.Returns(_root);
        _storage.IsInitialized.Returns(false);
    }

    private readonly string _root = Directory.CreateTempSubdirectory("configs-initializer-").FullName;
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly IAddressableState<TransactionOptions> _storage =
        Substitute.For<IAddressableState<TransactionOptions>>();

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    [Fact]
    public async Task FileInContentRoot_SeedsTheState()
    {
        File.WriteAllText(Path.Combine(_root, "config.transaction.json"), """{ "LockWaitSeconds": 9 }""");

        await ConfigsInitializer.Init(_environment, "config.transaction", _storage, NullLogger.Instance);

        await _storage.Received(1).SetValue(Arg.Is<TransactionOptions>(value => value.LockWaitSeconds == 9));
    }

    [Fact]
    public async Task FileMissingInContentRoot_ThrowsWithoutSeeding()
    {
        var init = () => ConfigsInitializer.Init(_environment, "config.transaction", _storage, NullLogger.Instance);

        await init.Should().ThrowAsync<FileNotFoundException>().WithMessage("*config.transaction*");
        await _storage.DidNotReceive().SetValue(Arg.Any<TransactionOptions>());
    }

    [Fact]
    public async Task StateInitialized_KeepsTheStoredValueWithoutAFile()
    {
        _storage.IsInitialized.Returns(true);

        await ConfigsInitializer.Init(_environment, "config.transaction", _storage, NullLogger.Instance);

        await _storage.DidNotReceive().SetValue(Arg.Any<TransactionOptions>());
    }
}
