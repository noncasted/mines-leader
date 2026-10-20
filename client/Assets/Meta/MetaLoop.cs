using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface IMetaConnection
    {
        // A lost meta socket (a dropped network, a restarted server) is opened again as the same user before a
        // request of the menu. Nothing is done while it is alive; concurrent callers share one reconnect. A failed
        // reconnect throws (the caller shows the loss; the next call tries again).
        UniTask EnsureConnected(IReadOnlyLifetime lifetime);
    }

    public class MetaLoop : IScopeBaseSetupAsync, IMetaConnection
    {
        public MetaLoop(
            IAuthentication authentication,
            IMetaBackend backend,
            IMetaState state,
            IStartupAssetsPreload startupAssets,
            IReadOnlyList<IMetaRegistry> registries,
            IBackendProjection<SharedBackendUser.ProfileProjection> profile,
            IClientOutdatedNotice outdatedNotice)
        {
            _authentication = authentication;
            _backend = backend;
            _state = state;
            _startupAssets = startupAssets;
            _registries = registries;
            _profile = profile;
            _outdatedNotice = outdatedNotice;
        }

        // A failed first connect is tried again after this pause (the server may be restarting).
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

        // The server sends the projections right after the connect: none in this time - the connection is not alive.
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(20);

        private readonly IAuthentication _authentication;
        private readonly IMetaBackend _backend;
        private readonly IMetaState _state;
        private readonly IStartupAssetsPreload _startupAssets;
        private readonly IReadOnlyList<IMetaRegistry> _registries;
        private readonly IBackendProjection<SharedBackendUser.ProfileProjection> _profile;
        private readonly IClientOutdatedNotice _outdatedNotice;

        private IReadOnlyLifetime _scopeLifetime;
        private ILifetime _connectionLifetime;
        private Guid _userId;
        private UniTaskCompletionSource _reconnect;

        public UniTask OnBaseSetupAsync(IReadOnlyLifetime lifetime)
        {
            _scopeLifetime = lifetime;

            // Подключение и реестры сетап не держат: меню грузится параллельно с ними,
            // а готовности меты дожидается само (см. MenuLoop).
            Connect(lifetime).Forget();
            LoadRegistries(lifetime).Forget();

            return UniTask.CompletedTask;
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

        private async UniTask Connect(IReadOnlyLifetime lifetime)
        {
            // The server may be down or restarting at the start: the first connect is retried until it succeeds,
            // the menu waits on its loading screen meanwhile.
            while (true)
            {
                try
                {
                    var isAuthorized = await Authorize(lifetime);

                    // Флаги ветки ставятся после закрытия её отрезков: за последним из них синхронно
                    // открывается меню и закрывает всю трассу.
                    if (isAuthorized == true)
                        _state.SetAuthorized();

                    return;
                }
                // The explicit retry: whatever fails here (no network, the socket lost, no projections) is tried
                // again. Only the end of the meta scope stops it.
                catch (Exception exception) when (lifetime.IsTerminated == false)
                {
                    // The server closed the socket as one of an outdated client: no retry can succeed, the notice
                    // with the way to update covers the menu.
                    if (_outdatedNotice.IsShown == true)
                    {
                        Debug.LogWarning("[Meta] [Loop] The client is outdated: the meta connection is not retried");
                        return;
                    }

                    Debug.LogWarning($"[Meta] [Loop] Meta connection failed, next attempt in {RetryDelay.TotalSeconds} s: " +
                                     $"{exception.GetType().Name} {exception.Message}");
                }

                await UniTask.Delay(RetryDelay, DelayType.Realtime, cancellationToken: lifetime.Token);
            }
        }

        private async UniTask<bool> Authorize(IReadOnlyLifetime lifetime)
        {
            Debug.Log("[Meta] [Loop] Starting meta connection");

            // Ветка идёт параллельно основной загрузке, поэтому её отрезок лежит в корне трассы
            // и на стек не встаёт: иначе этапы меню вложились бы в ожидание сокета. Текущим он
            // делается только на синхронных кусках, где вложенные замеры успевают его забрать.
            using var stage = GameProfiler.Detached("Meta connection");

            // Авторизация уехала в query запроса на апгрейд сокета: отдельного http-запроса
            // и отдельного кадра с хендшейком на старте больше нет.
            var savedUserId = _authentication.Load();
            var connectionLifetime = lifetime.Child();
            _connectionLifetime = connectionLifetime;

            // A connection that did not get its projections is not alive: a failed attempt ends it.
            try
            {
                // Подписка на проекции — до коннекта, чтобы отрезок ожидания не пропустил их приезд.
                var projectionsTask = _state.IsProjectionsReceived.WaitTrue(lifetime);
                var loss = new ConnectionLoss(_backend.Connection, connectionLifetime);

                var connect = stage.Child("Connect");
                connect.Start();

                UniTask connectTask;

                using (GameProfiler.Ambient(connect))
                    connectTask = _backend.Connect(connectionLifetime, savedUserId);

                await connect.Track(connectTask);

                Debug.Log("[Meta] [Loop] Waiting for projections");

                // Проекции приезжают с бэкенда пачкой после коннекта: этот отрезок и есть
                // ожидание данных, без которых меню открывать нечем.
                await stage.Measure("Projections", loss.Race(projectionsTask));
            }
            catch
            {
                connectionLifetime.Terminate();
                throw;
            }

            // Кто мы такие, говорит профильная проекция: сохранённого id могло не быть
            // вовсе, и тогда сервер завёл нового юзера прямо на коннекте.
            var profile = _profile.Value;

            if (profile == null)
            {
                connectionLifetime.Terminate();
                Debug.LogError("[Meta] [Loop] Profile projection is missing, user is not initialized");
                return false;
            }

            if (profile.Id != savedUserId)
            {
                using (GameProfiler.Ambient(stage))
                    _authentication.Save(profile.Id);
            }

            _userId = profile.Id;

            Debug.Log("[Meta] [Loop] Meta connection completed successfully: " + profile.Id);
            return true;
        }

        private async UniTaskVoid Reconnect(UniTaskCompletionSource completion)
        {
            Debug.Log("[Meta] [Loop] The meta connection is lost: connecting again");

            // The connection of the lost socket goes first: the reader, the writer and the commands run again on it.
            _connectionLifetime.Terminate();
            var connectionLifetime = _scopeLifetime.Child();
            _connectionLifetime = connectionLifetime;

            try
            {
                // The server sends the projections again on every connect; the profile tells who we are.
                var profileTask = _profile.WaitOnce(connectionLifetime);
                var loss = new ConnectionLoss(_backend.Connection, connectionLifetime);

                await _backend.Connect(connectionLifetime, _userId);
                var profile = await loss.Race(profileTask);

                // The server makes a new user for an id it does not know: the meta state of this client is of the old one.
                if (profile.Id != _userId)
                    throw new InvalidOperationException($"[Meta] [Loop] The meta reconnected as {profile.Id}, not {_userId}");

                Debug.Log("[Meta] [Loop] The meta connection is restored: " + _userId);
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

        private async UniTask LoadRegistries(IReadOnlyLifetime lifetime)
        {
            using (var stage = GameProfiler.Detached("Meta registries"))
            {
                // Реестры раскладывают спрайты по определениям, а спрайты качаются со старта.
                await stage.Measure("Startup assets", _startupAssets.IsLoaded.WaitTrue(lifetime));

                using (GameProfiler.Ambient(stage))
                using (GameProfiler.Scope("Initialize"))
                {
                    foreach (var registry in _registries)
                        registry.Initialize();
                }
            }

            _state.SetRegistriesLoaded();
        }

        // The loss of one meta connection while it is being opened. Subscribed before the socket opens: the loss may
        // be reported inside Connect. Race fails the wait for the first data with the loss, or after ReceiveTimeout.
        private class ConnectionLoss
        {
            public ConnectionLoss(INetworkConnection connection, IReadOnlyLifetime lifetime)
            {
                connection.Closed.Advise(lifetime, info => {
                    _info = info;
                    _lost.TrySetResult();
                });
            }

            private readonly UniTaskCompletionSource _lost = new();
            private SocketCloseInfo _info;

            public async UniTask Race(UniTask task)
            {
                var winner = await UniTask.WhenAny(task, _lost.Task).Timeout(ReceiveTimeout, DelayType.Realtime);

                if (winner == 1)
                    throw Lost();
            }

            public async UniTask<T> Race<T>(UniTask<T> task)
            {
                var (hasResult, result) = await UniTask.WhenAny(task, _lost.Task).Timeout(ReceiveTimeout, DelayType.Realtime);

                if (hasResult == false)
                    throw Lost();

                return result;
            }

            private Exception Lost()
            {
                return new Exception($"[Meta] [Loop] The meta connection is lost while connecting: {_info}");
            }
        }
    }
}
