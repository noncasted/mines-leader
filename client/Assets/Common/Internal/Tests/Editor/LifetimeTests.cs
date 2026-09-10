using System;
using System.Collections.Generic;
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

            // Список уже забран на момент удаления, поэтому слушатель ещё вызывается.
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
    }
}