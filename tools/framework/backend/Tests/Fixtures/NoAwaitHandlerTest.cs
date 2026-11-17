using Common.Extensions;

namespace Framework.Tests.Fixtures;

// Replaces the process-wide handler of NoAwaitExceptions for one test and puts the previous one back after it: a test
// that derives it belongs to NoAwaitCollection.
public abstract class NoAwaitHandlerTest : LifetimeTest
{
    protected NoAwaitHandlerTest()
    {
        _previous = NoAwaitExceptions.Handler;
        NoAwaitExceptions.SetHandler(e => {
            lock (_reported)
                _reported.Add(e);
        });
    }

    private readonly Action<Exception>? _previous;
    private readonly List<Exception> _reported = [];

    protected IReadOnlyList<Exception> Reported
    {
        get
        {
            lock (_reported)
                return _reported.ToList();
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        NoAwaitExceptions.SetHandler(_previous);
    }
}
