using System;
using Cysharp.Threading.Tasks;
using Global.Telemetry;
using Internal;
using Shared;

namespace Meta
{
    // Opens a lost meta socket again as the user of the first connect (MetaLoop), on the request of the menu.
    public class MetaReconnection : IMetaConnection, IMetaReconnection
    {
        public MetaReconnection(
            IAuthentication authentication,
            IMetaBackend backend,
            IBackendProjection<SharedBackendUser.ProfileProjection> profile,
            IClientOutdatedNotice outdatedNotice,
            IClientTelemetry telemetry,
            ILog<MetaReconnection> log)
        {
            _log = log;
            _authentication = authentication;
            _backend = backend;
            _profile = profile;
            _outdatedNotice = outdatedNotice;
            _telemetry = telemetry;
        }

        private readonly ILog _log;
        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IBackendProjection<SharedBackendUser.ProfileProjection> _profile;
        private readonly IClientOutdatedNotice _outdatedNotice;
        private readonly IClientTelemetry _telemetry;

        private IReadOnlyLifetime _scopeLifetime;
        private ILifetime _connectionLifetime;
        private Guid _userId;
        private UniTaskCompletionSource _reconnect;

        public void OnConnected(IReadOnlyLifetime scopeLifetime, ILifetime connection, Guid userId)
        {
            if (_userId != Guid.Empty)
                throw new InvalidOperationException($"[Meta] [Reconnection] The meta is connected twice: {_userId}, {userId}");

            _scopeLifetime = scopeLifetime;
            _connectionLifetime = connection;
            _userId = userId;
        }

        public async UniTask EnsureConnected(IReadOnlyLifetime lifetime)
        {
            // The server runs another protocol: every connect is refused the same way, the notice is already shown.
            if (_outdatedNotice.IsShown == true)
                throw new ClientOutdatedException("[Meta] [Loop] The client is outdated: the meta is not reconnected");

            if (_userId == Guid.Empty)
                throw new InvalidOperationException("[Meta] [Loop] The meta is reconnected before its first authorization");

            // One reconnect at a time, shared by every caller: a second connect would replace the first one on the
            // server, a request sent meanwhile would go to a closed connection.
            var reconnect = _reconnect;

            if (reconnect == null)
            {
                if (_backend.Connection.Writer.IsClosed == false)
                    return;

                reconnect = new UniTaskCompletionSource();
                _reconnect = reconnect;
                Reconnect(reconnect).Forget();
            }

            // The caller may give up meanwhile: the connect goes on, the connection stays the one of the meta scope.
            await reconnect.Task.AttachExternalCancellation(lifetime.Token);
        }

        private async UniTaskVoid Reconnect(UniTaskCompletionSource completion)
        {
            _log.Info("The meta connection is lost: connecting again");

            // The connection of the lost socket goes first: the reader, the writer and the commands run again on it.
            _connectionLifetime.Terminate();
            var connectionLifetime = _scopeLifetime.Child();
            _connectionLifetime = connectionLifetime;

            try
            {
                // The server sends the projections again on every connect.
                var profileTask = _profile.WaitOnce(connectionLifetime);
                var loss = new MetaConnectionLoss(_backend.Connection, connectionLifetime);

                // The guest saved by the first connect. The server makes a new guest for an id it does not know (a
                // wiped database): the meta state of this client is of the old one, the reconnect fails.
                var response = await _backend.Connect(connectionLifetime, MetaSocketAuth.CreateRequest(_authentication, _log));

                if (response.UserId != _userId)
                    throw new InvalidOperationException($"[Meta] [Loop] The meta reconnected as {response.UserId}, not {_userId}");

                await loss.Race(profileTask);

                _log.Info("The meta connection is restored: {UserId}", _userId);
                // A new connection: the id of the startup record of the previous one is not valid on it.
                _telemetry.SendStartupTrace();
                _reconnect = null;
                completion.TrySetResult();
            }
            // A connection that is not restored is not alive: a failed attempt (the socket lost or not opened, no
            // projections) ends it, so the next call connects again instead of taking it for the meta. The failure
            // goes to the callers.
            catch (Exception exception)
            {
                connectionLifetime.Terminate();
                _reconnect = null;
                completion.TrySetException(exception);
            }
        }
    }
}
