using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // ConnectionWriter: the send loop and ForceSendAll send a message once; the writer runs again after a loss.
    // N4 (port of peace-data-reborn F2.3/F2.16): WriteRequest never returns null - the typed response,
    // NetworkRequestException for ErrorResponse, InvalidOperationException for a response of another type or a request
    // after ForceSendAll, OperationCanceledException at once (not after the 20 s timeout) when the connection is lost
    // or its lifetime ends. The 20 s timeout is not covered (real time).
    public class ConnectionWriterTests
    {
        // The send loop picks the queue up within a few frames: shows the loop runs in the test, so the check of
        // ForceSendAll below is real.
        [UnityTest]
        public IEnumerator Loop_SendsQueuedMessage() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await Frames();

            Assert.AreEqual(1, socket.Sent.Count);
            lifetime.Terminate();
        });

        // ForceSendAll takes the queue: the send loop does not send the same messages a second time.
        [UnityTest]
        public IEnumerator ForceSendAll_LoopDoesNotSendQueueAgain() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await writer.ForceSendAll();
            await Frames();

            Assert.AreEqual(1, socket.Sent.Count);
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator SocketClosed_PendingAndNextRequestsAreCancelledAtOnce() => UniTask.ToCoroutine(async () =>
        {
            var (writer, socket, lifetime) = Create();

            var pending = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            socket.ClosedSource.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, "lost"));

            Assert.IsInstanceOf<OperationCanceledException>(await Catch(pending));
            Assert.IsTrue(writer.IsClosed);

            var next = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());

            Assert.IsTrue(next.Status.IsCompleted());
            Assert.IsInstanceOf<OperationCanceledException>(await Catch(next));
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator LifetimeEnds_PendingRequestIsCancelled() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var pending = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            lifetime.Terminate();

            Assert.IsInstanceOf<OperationCanceledException>(await Catch(pending));
        });

        [UnityTest]
        public IEnumerator Request_ErrorResponse_ThrowsNetworkRequestException() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            writer.OnRequestHandled(new ErrorResponse { Code = ErrorCode.Forbidden, Message = "no" }, 1);

            var exception = await Catch(request);

            Assert.IsInstanceOf<NetworkRequestException>(exception);
            Assert.AreEqual(ErrorCode.Forbidden, ((NetworkRequestException)exception).Code);
            Assert.AreEqual("SharedSessionAuth.Request", ((NetworkRequestException)exception).RequestName);
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator Request_OtherResponseType_ThrowsInvalidOperation() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            writer.OnRequestHandled(new EmptyResponse(), 1);

            Assert.IsInstanceOf<InvalidOperationException>(await Catch(request));
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator ForceSendAll_ThenRequestThrowsInvalidOperation() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            await writer.ForceSendAll();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            Assert.IsInstanceOf<InvalidOperationException>(await Catch(request));
            lifetime.Terminate();
        });

        [UnityTest]
        public IEnumerator Request_Answered_ReturnsResponse() => UniTask.ToCoroutine(async () =>
        {
            var (writer, _, lifetime) = Create();

            var request = writer.WriteRequest<SharedSessionAuth.Response>(new SharedSessionAuth.Request());
            writer.OnRequestHandled(new SharedSessionAuth.Response { Status = SessionAuthStatus.NoReservation }, 1);

            Assert.AreEqual(SessionAuthStatus.NoReservation, (await request).Status);
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

            lostSocket.ClosedSource.Invoke(new SocketCloseInfo(SocketCloseInfo.AbnormalClosure, "lost"));
            Assert.IsTrue(writer.IsClosed);
            lostLifetime.Terminate();

            var socket = new FakeSocket();
            var lifetime = new Lifetime();
            writer.Run(lifetime, socket);

            Assert.IsFalse(writer.IsClosed);

            writer.WriteOneWay(new SharedSessionAuth.Response { Status = SessionAuthStatus.Ok });
            await Frames();

            Assert.AreEqual(1, socket.Sent.Count);
            Assert.AreEqual(0, lostSocket.Sent.Count);
            lifetime.Terminate();
        });

        [Test]
        public void RunAgainWhileAlive_Throws()
        {
            var (writer, _, lifetime) = Create();

            Assert.Throws<InvalidOperationException>(() => writer.Run(lifetime, new FakeSocket()));
            lifetime.Terminate();
        }

        private static async UniTask<Exception> Catch<T>(UniTask<T> task)
        {
            try
            {
                await task;
            }
            catch (Exception exception)
            {
                return exception;
            }

            Assert.Fail("The request was expected to fail");
            return null;
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
            public readonly ViewableDelegate<SocketCloseInfo> ClosedSource = new();
            public readonly List<byte[]> Sent = new();

            public IViewableDelegate<byte[]> Received { get; } = new ViewableDelegate<byte[]>();
            public IViewableDelegate<SocketCloseInfo> Closed => ClosedSource;

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
