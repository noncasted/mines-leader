using Common.Extensions;
using Npgsql;

namespace Framework.Tests.MatchMetrics;

// The database of the tests that can go down, and a step of the test run on the next connection a writer opens.
public sealed class SwitchableDbSource : IDbSource
{
    public SwitchableDbSource(IDbSource inner)
    {
        _inner = inner;
    }

    private readonly IDbSource _inner;
    private volatile bool _isDown;
    private int _failures;
    private Action? _onNextAccess;

    public int Failures => Volatile.Read(ref _failures);

    public NpgsqlDataSource Value
    {
        get
        {
            Interlocked.Exchange(ref _onNextAccess, null)?.Invoke();

            if (_isDown)
            {
                Interlocked.Increment(ref _failures);
                throw new NpgsqlException("The database of the test is down");
            }

            return _inner.Value;
        }
    }

    public void Fail() => _isDown = true;
    public void Recover() => _isDown = false;
    public void OnNextAccess(Action action) => _onNextAccess = action;
}
