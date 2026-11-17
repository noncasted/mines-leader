using System.Collections.Concurrent;
using Internal;
using Xunit;

namespace Framework.Tests.Fixtures;

// The circuits of the console subscribe to a singleton and leave it on their own threads while the host sets its value on
// another one. The subscribers that stay get the last value, nothing throws.
public static class SubscriptionStress
{
    private const int Circuits = 6;
    private const int Visits = 400;
    private const int Writers = 2;
    private const int Writes = 2000;
    private const int KeptPerCircuit = 8;
    private const int KeepEvery = Visits / KeptPerCircuit;

    // set(n) gives the value n; a larger n is a newer value. read(value) gives back expected(n), n itself by default.
    public static async Task Run<T>(Action<IReadOnlyLifetime, Action<T>> view, Action<int> set, Func<T, int> read, Func<int, int>? expected = null)
    {
        var lifetime = new Lifetime();
        var errors = new ConcurrentQueue<Exception>();
        var kept = new int[Circuits * KeptPerCircuit];
        var next = 0;
        using var gate = new ManualResetEventSlim(false);

        try
        {
            var circuits = Enumerable.Range(0, Circuits).Select(circuit => Start(gate, errors, () =>
            {
                for (var visit = 0; visit < Visits; visit++)
                {
                    var page = lifetime.Child();
                    view(page, _ => { });

                    if (visit % KeepEvery == 0)
                    {
                        var slot = circuit * KeptPerCircuit + visit / KeepEvery;
                        view(lifetime, value => Volatile.Write(ref kept[slot], read(value)));
                    }

                    page.Terminate();
                }
            }));

            var writers = Enumerable.Range(0, Writers).Select(_ => Start(gate, errors, () =>
            {
                for (var write = 0; write < Writes; write++)
                    set(Interlocked.Increment(ref next));
            }));

            var all = circuits.Concat(writers).ToArray();
            gate.Set();
            await Task.WhenAll(all);

            var last = Interlocked.Increment(ref next);
            set(last);

            Assert.Empty(errors);

            Assert.All(kept, value => Assert.Equal(expected?.Invoke(last) ?? last, value));
        }
        finally
        {
            lifetime.Terminate();
        }
    }

    private static Task Start(ManualResetEventSlim gate, ConcurrentQueue<Exception> errors, Action work)
    {
        return Task.Factory.StartNew(() =>
        {
            gate.Wait();

            try
            {
                work();
            }
            catch (Exception e)
            {
                errors.Enqueue(e);
            }
        }, TaskCreationOptions.LongRunning);
    }
}
