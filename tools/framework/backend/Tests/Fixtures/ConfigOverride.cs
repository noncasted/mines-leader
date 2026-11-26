using Infrastructure;
using NSubstitute;

namespace Framework.Tests.Fixtures;

// Other options of a config mock of the fixture until disposed. The tests of a collection run one at a time, so the
// other tests keep the values of the fixture, which stay wide enough for a loaded machine.
public sealed class ConfigOverride<TOptions> : IDisposable
    where TOptions : class, new()
{
    public ConfigOverride(IAddressableState<TOptions> config, TOptions options)
    {
        _config = config;
        _previous = config.Value;
        config.Value.Returns(options);
    }

    private readonly IAddressableState<TOptions> _config;
    private readonly TOptions _previous;

    public void Dispose()
    {
        _config.Value.Returns(_previous);
    }
}
