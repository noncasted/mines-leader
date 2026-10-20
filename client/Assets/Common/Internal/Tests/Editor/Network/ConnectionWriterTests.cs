using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // ConnectionWriter: the send loop and ForceSendAll send a message once; a lost connection answers pending and
    // next requests with null at once (not after the 20 s timeout); the writer runs again after a loss.
    public class ConnectionWriterTests
    {
        // The send loop picks the queue up within a few frames: shows the loop runs in the test, so the check of
        // ForceSendAll below is real.
        [UnityTest]
        public IEnumerator Loop_SendsQueuedMessage() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            writer.WriteOneWay(new SharedSessionAuth.Response { IsSuccess = true });
            await Frames();

            Assert.AreEqual(1, socket.Sent.Count);
            lifetime.Terminate();
        });

        // ForceSendAll takes the queue: the send loop does not send the same messages a second time.
        [UnityTest]
        public IEnumerator ForceSendAll_LoopDoesNotSendQueueAgain() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            writer.WriteOneWay(new SharedSessionAuth.Response { IsSuccess = true });
            await writer.ForceSendAll();
            await Frames();

            Assert.AreEqual(1, socket.Sent.Count);
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator SocketClosed_PendingAndNextRequestsGetNullAtOnce() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            LogAssert.Expect(LogType.Error, new Regex("got no response"));
            var pending = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            socket.ClosedSource.Invoke("lost");

            Assert.IsNull(await pending);
            Assert.IsTrue(writer.IsClosed);

            LogAssert.Expect(LogType.Error, new Regex("the connection is closed"));
            var next = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());

            Assert.IsTrue(next.Status.IsCompleted());
            Assert.IsNull(await next);
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator Request_Answered_ReturnsResponse() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            writer.OnRequestHandled(new SharedSessionAuth.Response { IsSuccess = true }, 1);

            Assert.IsTrue((await request).IsSuccess);
            lifetime.Terminate();
        });

        [Test]
        public void NeverRun_IsClosed()
        {
            Assert.IsTrue(new ConnectionWriter().IsClosed);
        }

        // The meta reconnect runs the same writer on a new socket: the closed state of the lost one does not stay.
        [UnityTest]
        public IEnumerator RunAgainAfterLoss_SendsToNewSocket() => UniTask.ToCoroutine(async () =>
        {
            var (writer, lostSocket, lostLifetime) = Create();

            lostSocket.ClosedSource.Invoke("lost");
            Assert.IsTrue(writer.IsClosed);
            lostLifetime.Terminate();

            var socket = new FakeSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, socket);

            Assert.IsFalse(writer.IsClosed);

            writer.WriteOneWay(new SharedSessionAuth.Response { IsSuccess = true });
            await Frames();

            Assert.AreEqual(1, socket.Sent.Count);
            Assert.AreEqual(0, lostSocket.Sent.Count);
            lifetime.Terminate();
        });

        [Test]
        public void RunAgainWhileAlive_Throws()
        {
            var (writer, _, lifetime) = Create();

            Assert.Throws<System.InvalidOperationException>(() => writer.Run(lifetime, new FakeSocket()));
            lifetime.Terminate();
        }

        private static (ConnectionWriter, FakeSocket, Lifetime) Create()
        {
            var writer = new ConnectionWriter();
            var socket = new FakeSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, socket);
            return (writer, socket, lifetime);
        }

        // The idle send loop sleeps 50 ms between checks of the queue.
        private static UniTask Frames()
        {
            return UniTask.Delay(200, DelayType.Realtime);
        }

        private class FakeSocket : IWebSocket
        {
            public readonly ViewableDelegate<string> ClosedSource = new();
            public readonly List<byte[]> Sent = new();

            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<string> Closed => ClosedSource;

            public UniTask Connect()
            {
                return UniTask.CompletedTask;
            }

            public UniTask Send(byte[] bytes)
            {
                Sent.Add(bytes);
                return UniTask.CompletedTask;
            }
        }
    }
}
