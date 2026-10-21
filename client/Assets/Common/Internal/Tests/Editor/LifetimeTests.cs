using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    public class LifetimeTests
    {
        [Test]
        public void Terminate_InvokesAllListenersOnce()
        {
            var lifetime = new Lifetime();
            var calls = new List<int>();

            lifetime.Listen(() => calls.Add(1));
            lifetime.Listen(() => calls.Add(2));

            lifetime.Terminate();
            lifetime.Terminate();

            CollectionAssert.AreEqual(new[] { 1, 2 }, calls);
            Assert.IsTrue(lifetime.IsTerminated);
        }

        [Test]
        public void Terminate_ListenerThrows_OthersStillRun()
        {
            var lifetime = new Lifetime();
            var secondCalled = false;

            lifetime.Listen(() => throw new InvalidOperationException("boom"));
            lifetime.Listen(() => secondCalled = true);

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));

            lifetime.Terminate();

            Assert.IsTrue(secondCalled);
        }

        [Test]
        public void Listen_DuringTerminate_IsInvokedImmediately()
        {
            var lifetime = new Lifetime();
            var lateCalled = false;

            lifetime.Listen(() => lifetime.Listen(() => lateCalled = true));

            LogAssert.Expect(LogType.Error, "Trying to listen terminated lifetime");

            lifetime.Terminate();

            Assert.IsTrue(lateCalled);
        }

        [Test]
        public void RemoveListener_DuringTerminate_DoesNotThrow()
        {
            var lifetime = new Lifetime();
            var removedCalled = false;

            void Removed() => removedCalled = true;

            lifetime.Listen(() => lifetime.RemoveListener(Removed));
            lifetime.Listen(Removed);

            lifetime.Terminate();

            // The list is already taken at the time of removal, so the listener is still called.
            Assert.IsTrue(removedCalled);
        }

        [Test]
        public void Token_IsCancelledAfterTerminate()
        {
            var lifetime = new Lifetime();
            var token = lifetime.Token;

            Assert.IsFalse(token.IsCancellationRequested);

            lifetime.Terminate();

            Assert.IsTrue(token.IsCancellationRequested);
            Assert.IsTrue(lifetime.Token.IsCancellationRequested);
        }

        [Test]
        public void Token_RequestedAfterTerminate_IsCancelled()
        {
            var lifetime = new Lifetime();

            lifetime.Terminate();

            Assert.IsTrue(lifetime.Token.IsCancellationRequested);
        }

        [Test]
        public void Child_TerminatesWithParent()
        {
            var parent = new Lifetime();
            var child = parent.Child();

            parent.Terminate();

            Assert.IsTrue(child.IsTerminated);
        }

        [Test]
        public void Child_TerminatedEarly_DetachesFromParent()
        {
            var parent = new Lifetime();
            var child = parent.Child();
            var childCalls = 0;

            child.Listen(() => childCalls++);
            child.Terminate();
            parent.Terminate();

            Assert.AreEqual(1, childCalls);
        }

        [Test]
        public void Listen_Null_Throws()
        {
            var lifetime = new Lifetime();

            Assert.Throws<ArgumentNullException>(() => lifetime.Listen(null));
        }

        [Test]
        public void Intersect_EndsWithEitherSource()
        {
            var a = new Lifetime();
            var b = new Lifetime();

            var fromA = a.Intersect(b);
            a.Terminate();

            Assert.IsTrue(fromA.IsTerminated);

            var c = new Lifetime();
            var fromB = c.Intersect(b);
            b.Terminate();

            Assert.IsTrue(fromB.IsTerminated);
            Assert.IsFalse(c.IsTerminated);
        }

        [Test]
        public void Intersect_TerminatedDirectly_LeavesBothSources()
        {
            var a = new CountingLifetime();
            var b = new CountingLifetime();

            var intersection = a.Intersect(b);

            Assert.AreEqual(1, a.Count);
            Assert.AreEqual(1, b.Count);

            intersection.Terminate();

            Assert.AreEqual(0, a.Count);
            Assert.AreEqual(0, b.Count);
        }

        [Test]
        public void Intersect_WithTerminatedSource_IsTerminatedAndDoesNotSubscribeTheOther()
        {
            var a = new Lifetime();
            a.Terminate();
            var b = new CountingLifetime();

            LogAssert.Expect(LogType.Error, "Trying to listen terminated lifetime");

            var intersection = a.Intersect(b);

            Assert.IsTrue(intersection.IsTerminated);
            Assert.AreEqual(0, b.Count);
        }

        [UnityTest]
        public IEnumerator WaitInvoke_Invoked_CompletesAndUnsubscribesBoth() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new CountingLifetime();
            var source = new List<Action>();

            var wait = lifetime.WaitInvoke(source.Add, action => source.Remove(action));

            Assert.AreEqual(1, source.Count);
            Assert.AreEqual(1, lifetime.Count);

            source[0].Invoke();
            await wait;

            Assert.AreEqual(0, source.Count);
            Assert.AreEqual(0, lifetime.Count);
        });

        [UnityTest]
        public IEnumerator WaitInvoke_LifetimeEnds_CancelsAndUnsubscribes() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();
            var source = new List<Action>();

            var wait = lifetime.WaitInvoke(source.Add, action => source.Remove(action));
            lifetime.Terminate();

            var isCancelled = false;

            try
            {
                await wait;
            }
            catch (OperationCanceledException)
            {
                isCancelled = true;
            }

            Assert.IsTrue(isCancelled);
            Assert.AreEqual(0, source.Count);
        });

        // Terminate on one thread races with Listen and Token on others: every listener runs exactly once (by
        // Terminate or at once by Listen, which logs it), and every token handed out ends cancelled.
        [Test]
        public void Terminate_RacingWithListenAndToken_LosesNothing()
        {
            const int threads = 4;
            const int listenersPerThread = 200;

            LogAssert.ignoreFailingMessages = true;

            try
            {
                for (var round = 0; round < 50; round++)
                {
                    var lifetime = new Lifetime();
                    var calls = 0;
                    var tokens = new System.Collections.Concurrent.ConcurrentBag<CancellationToken>();

                    using var start = new ManualResetEventSlim(false);

                    var workers = new Task[threads + 1];

                    for (var t = 0; t < threads; t++)
                    {
                        workers[t] = Task.Run(() => {
                            start.Wait();

                            for (var i = 0; i < listenersPerThread; i++)
                            {
                                lifetime.Listen(() => Interlocked.Increment(ref calls));
                                tokens.Add(lifetime.Token);
                            }
                        });
                    }

                    workers[threads] = Task.Run(() => {
                        start.Wait();
                        lifetime.Terminate();
                    });

                    start.Set();
                    Task.WaitAll(workers);

                    Assert.AreEqual(threads * listenersPerThread, calls);

                    foreach (var token in tokens)
                        Assert.IsTrue(token.IsCancellationRequested);
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private sealed class CountingLifetime : IReadOnlyLifetime
        {
            private readonly List<Action> _listeners = new();

            public int Count => _listeners.Count;
            public CancellationToken Token => CancellationToken.None;
            public bool IsTerminated => false;

            public void Listen(Action callback)
            {
                _listeners.Add(callback);
            }

            public void RemoveListener(Action callback)
            {
                _listeners.Remove(callback);
            }
        }
    }
}
